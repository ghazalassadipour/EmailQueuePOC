using Microsoft.EntityFrameworkCore;
using EmailQueuePOC.Models;

namespace EmailQueuePOC.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<EmailQueueItem> EmailQueue { get; set; }
        public DbSet<EmailAttachment> EmailAttachments { get; set; } 

        public AppDbContext(DbContextOptions<AppDbContext> options) 
            : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // One-to-many: EmailQueueItem -> EmailAttachment
        modelBuilder.Entity<EmailAttachment>()
            .HasOne(a => a.EmailQueueItem)
            .WithMany(e => e.Attachments)
            .HasForeignKey(a => a.EmailQueueItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional: If you want a special table name or constraints
    }
    }
    
}
