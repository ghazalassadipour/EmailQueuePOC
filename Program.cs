using Hangfire;
using Hangfire.Storage.SQLite; // <-- Make sure you're using this namespace
using Microsoft.EntityFrameworkCore;
using EmailQueuePOC.Data;
using EmailQueuePOC.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(""));


// Add Hangfire with SQLite storage
builder.Services.AddHangfire(config =>
{
    // 'UseSQLiteStorage' is provided by the Hangfire.Storage.SQLite package
    config.UseSQLiteStorage("HangfireJobs.db");
});

builder.Services.AddHangfireServer();

builder.Services.AddScoped<EmailSenderJob>();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Optional Hangfire dashboard
app.UseHangfireDashboard();

// Recurring job example
app.Lifetime.ApplicationStarted.Register(() =>
{
    RecurringJob.AddOrUpdate<EmailSenderJob>(
        "SendPendingEmails",  // recurringJobId
        job => job.SendPendingEmails(),
        "* * * * *"           // run every minute
    );
});

app.MapControllers();
app.Run();
