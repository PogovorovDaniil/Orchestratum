using System.Collections.Concurrent;

namespace Orchestratum.Example.Services;

public class NotificationService
{
    private readonly ConcurrentBag<NotificationLog> _logs = [];

    public async Task SendEmailAsync(string email, string subject, string message)
    {
        await Task.Delay(200); // Simulate email sending

        _logs.Add(new NotificationLog
        {
            Timestamp = DateTime.UtcNow,
            Email = email,
            Subject = subject,
            Message = message
        });
    }

    public List<NotificationLog> GetLogs()
    {
        return _logs.OrderByDescending(l => l.Timestamp).ToList();
    }
}

public class NotificationLog
{
    public DateTime Timestamp { get; set; }
    public string Email { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
}
