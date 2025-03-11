using EmailQueuePOC.Data;
using EmailQueuePOC.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.EntityFrameworkCore;

namespace EmailQueuePOC.Services
{
    public class EmailSenderJob
    {
        private readonly AppDbContext _dbContext;
        private const int MAX_RETRIES = 3;

        // Throttle config
        private const int BATCH_SIZE = 10;       // how many to process per run
        private const int EMAILS_PER_MINUTE = 20; // time-based limit (example)

        // Track timestamps of sent emails for time-based throttling
        private static readonly Queue<DateTime> _sendTimestamps = new Queue<DateTime>();

        public EmailSenderJob(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SendPendingEmails()
        {
            // 1) Fetch some pending emails (up to BATCH_SIZE),
            // BUT also respect the time-based throttle (EMAILS_PER_MINUTE).
            var pendingEmails = await _dbContext.EmailQueue
                .Include(e => e.Attachments)
                .Where(e => e.Status == EmailStatus.Pending)
                .OrderBy(e => e.Priority)
                .ThenBy(e => e.CreatedAt)
                .Take(BATCH_SIZE)
                .ToListAsync();

            if (!pendingEmails.Any())
            {
                Console.WriteLine("No pending emails to process.");
                return;
            }

            Console.WriteLine($"Found {pendingEmails.Count} pending emails...");

            foreach (var email in pendingEmails)
            {
                // Check time-based limit
                if (!CanSendMoreEmails())
                {
                    Console.WriteLine("Throttling reached. Will stop processing for now.");
                    break;
                }

                await ProcessEmailAsync(email);
                RecordSend();
            }

            // Save changes after processing each batch
            await _dbContext.SaveChangesAsync();
        }

        private bool CanSendMoreEmails()
        {
            var now = DateTime.UtcNow;
            // Remove timestamps older than 1 minute
            while (_sendTimestamps.Count > 0 && _sendTimestamps.Peek() < now.AddMinutes(-1))
            {
                _sendTimestamps.Dequeue();
            }

            // If we sent less than EMAILS_PER_MINUTE in the last minute, we're good
            return _sendTimestamps.Count < EMAILS_PER_MINUTE;
        }

        private void RecordSend()
        {
            _sendTimestamps.Enqueue(DateTime.UtcNow);
        }

        private async Task ProcessEmailAsync(EmailQueueItem email)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("NoReply", "no-reply@vancouver.ca"));
                message.To.Add(MailboxAddress.Parse(email.RecipientEmail));
                message.Subject = email.Subject;

                // Build a multipart (HTML + possible attachments)
                var builder = new BodyBuilder
                {
                    HtmlBody = email.Body // or text body if you're not using HTML
                };

                // 1) Add attachments from the separate table
                if (email.Attachments != null && email.Attachments.Any())
                {
                    foreach (var attach in email.Attachments)
                    {
                        // If the file name is "citylogo.png" (or any condition you prefer),
                        // treat it as an inline image.
                        if (attach.FileName == "citylogo.png")
                        {
                            // 1) Add the linked resource
                            var entity = builder.LinkedResources.Add(
                                attach.FileName,
                                attach.FileBytes,
                                ContentType.Parse(attach.ContentType)
                            );

                            // 2) Cast to MimePart to access ContentTransferEncoding, etc.
                            var resourcePart = (MimePart)entity;

                            // 3) Mark it for inline display
                            resourcePart.ContentId = "citylogo";
                            resourcePart.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                            resourcePart.ContentTransferEncoding = ContentEncoding.Base64;
                        }
                        else
                        {
                            // Normal attachments
                            builder.Attachments.Add(
                                attach.FileName,
                                attach.FileBytes,
                                ContentType.Parse(attach.ContentType)
                            );
                        }
                    }
                }
                message.Body = builder.ToMessageBody();

                using (var smtpClient = new SmtpClient())
                {
                    // Connect to your SMTP server
                    await smtpClient.ConnectAsync("smtp.vancouver.ca", 25, SecureSocketOptions.None);
                    // If no auth needed, skip
                    await smtpClient.SendAsync(message);
                    await smtpClient.DisconnectAsync(true);
                }

                email.Status = EmailStatus.Sent;
                email.LastAttemptAt = DateTime.UtcNow;
                Console.WriteLine($"Email sent successfully to {email.RecipientEmail}");
            }
            catch (Exception ex)
            {
                email.RetryCount++;
                email.LastAttemptAt = DateTime.UtcNow;

                if (email.RetryCount >= MAX_RETRIES)
                {
                    email.Status = EmailStatus.Failed;
                    Console.WriteLine($"Email permanently failed to {email.RecipientEmail}. Error: {ex.Message}");
                }
                else
                {
                    // Keep it Pending for next attempt
                    email.Status = EmailStatus.Pending;
                    Console.WriteLine($"Email sending failed (attempt {email.RetryCount}) for {email.RecipientEmail}: {ex.Message}");
                }
            }
        }
    }
}
