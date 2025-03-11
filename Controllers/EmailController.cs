using EmailQueuePOC.Data;
using EmailQueuePOC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmailQueuePOC.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public EmailController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost("bulk")]
        public async Task<IActionResult> EnqueueBulkEmails([FromBody] BulkEmailRequest request)
        {
            if (request.Emails == null || !request.Emails.Any())
                return BadRequest("No emails to queue.");

            var newItems = request.Emails.Select(dto =>
            {
                // Create the main queue item
                var queueItem = new EmailQueueItem
                {
                    RecipientEmail = dto.RecipientEmail,
                    Subject        = dto.Subject,
                    Body           = dto.Body,
                    Priority       = dto.Priority,
                    Status         = EmailStatus.Pending,
                    CreatedAt      = DateTime.UtcNow
                };

                // Create a list of attachment entities (if any)
                if (dto.Attachments != null && dto.Attachments.Any())
                {
                    queueItem.Attachments = dto.Attachments.Select(a => new EmailAttachment
                    {
                        FileName    = a.FileName,
                        ContentType = a.ContentType,

                        // Convert from base64 to raw bytes
                        FileBytes   = !string.IsNullOrEmpty(a.Base64Content)
                                      ? Convert.FromBase64String(a.Base64Content)
                                      : Array.Empty<byte>()
                    }).ToList();
                }

                return queueItem;
            }).ToList();

            // Save to DB
            await _dbContext.EmailQueue.AddRangeAsync(newItems);
            await _dbContext.SaveChangesAsync();

            return Ok(new 
            {
                Message = $"Queued {newItems.Count} emails.",
                Count = newItems.Count
            });
        }
    }

   public class BulkEmailRequest
{
    public List<EmailDto> Emails { get; set; } = new();
}

public class EmailDto
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject        { get; set; } = string.Empty;
    public string Body           { get; set; } = string.Empty;
    public int Priority          { get; set; } = 5; // optional

    // Zero or more attachments, each with base64
    public List<EmailAttachmentDto>? Attachments { get; set; }
}

// CHANGED: FileContent -> Base64Content
public class EmailAttachmentDto
{
    public string FileName    { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf"; 
    public string Base64Content { get; set; } = string.Empty; 
}

}
