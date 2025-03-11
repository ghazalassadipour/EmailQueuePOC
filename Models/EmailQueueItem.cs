using System.ComponentModel.DataAnnotations;

namespace EmailQueuePOC.Models
{
    public enum EmailStatus
    {
        Pending,
        Sent,
        Failed
    }

    public class EmailQueueItem
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string RecipientEmail { get; set; } = string.Empty;
        [Required]
        public string Subject { get; set; } = string.Empty;
        [Required]
        public string Body { get; set; } = string.Empty;

        public EmailStatus Status { get; set; } = EmailStatus.Pending;
        public int RetryCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastAttemptAt { get; set; }
        public int Priority { get; set; }
        public List<EmailAttachment>? Attachments { get; set; }
    }
   public class EmailAttachment
{
    public int Id { get; set; }

    // The attachment data
    public string FileName    { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public byte[] FileBytes   { get; set; } = Array.Empty<byte>();

    // Foreign Key relationship
    public int EmailQueueItemId { get; set; }

    // Navigation property back to the EmailQueueItem
    public EmailQueueItem? EmailQueueItem { get; set; }
}

}
