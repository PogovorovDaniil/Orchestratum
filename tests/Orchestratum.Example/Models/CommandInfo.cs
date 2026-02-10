namespace Orchestratum.Example.Models;

public class CommandInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public DateTime ScheduledAt { get; set; }
    public DateTime? RunningAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsRunning { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsFailed { get; set; }
    public bool IsCanceled { get; set; }
    public int RetriesLeft { get; set; }
    public TimeSpan Timeout { get; set; }
}
