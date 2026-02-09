using Microsoft.EntityFrameworkCore;
using Orchestratum.Database;
using Orchestratum.Tests.Misc;
using System.Collections.Concurrent;
using Xunit;

namespace Orchestratum.Tests;

public class OrchestratumUsageExamplesFixture : TestApplication
{
    private readonly ConcurrentBag<string> _log = [];

    public void ClearLog() => _log.Clear();
    public List<string> GetLog() => _log.ToList();

    public override void ConfigureOrchestratum(IServiceProvider serviceProvider, OrchestratumConfiguration configuration)
    {
        configuration.RegisterExecutor("send-email", async (sp, data, ct) =>
        {
            var email = (OrchestratumUsageExamples.SendEmailCommand)data;
            _log.Add($"Email sent to {email.To}: {email.Subject}");
            await Task.CompletedTask;
        });

        configuration.RegisterExecutor("process-video", async (sp, data, ct) =>
        {
            var video = (OrchestratumUsageExamples.ProcessVideoCommand)data;
            _log.Add($"Processing video: {video.VideoId}");
            await Task.Delay(video.ProcessingTimeMs, ct);
            _log.Add($"Video {video.VideoId} processed");
        });

        configuration.RegisterExecutor("call-external-api", async (sp, data, ct) =>
        {
            var apiCall = (OrchestratumUsageExamples.ApiCallCommand)data;
            if (apiCall.ShouldFail)
            {
                _log.Add($"API call failed: {apiCall.Endpoint}");
                throw new HttpRequestException("API unavailable");
            }
            _log.Add($"API call success: {apiCall.Endpoint}");
            await Task.CompletedTask;
        });

        configuration.RegisterExecutor("process-with-di", async (sp, data, ct) =>
        {
            var command = (OrchestratumUsageExamples.ProcessCommand)data;
            _log.Add($"Processing with DI: {command.Data}");
            await Task.CompletedTask;
        });
    }
}

/// <summary>
/// Примеры использования Orchestratum через интеграционные тесты.
/// Демонстрируют основные паттерны работы с библиотекой.
/// </summary>
public class OrchestratumUsageExamples : IClassFixture<OrchestratumUsageExamplesFixture>, IAsyncLifetime
{
    private readonly OrchestratumUsageExamplesFixture _fixture;

    public OrchestratumUsageExamples(OrchestratumUsageExamplesFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _fixture.ClearLog();
        await _fixture.CleanDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public record SendEmailCommand(string To, string Subject, string Body);
    public record ProcessVideoCommand(string VideoId, int ProcessingTimeMs);
    public record ApiCallCommand(string Endpoint, bool ShouldFail);
    public record ProcessCommand(string Data);

    [Fact]
    public async Task Example_SimpleEmailSending()
    {
        var email = new SendEmailCommand("user@example.com", "Welcome", "Welcome to our service!");
        await _fixture.Orchestratum.Append("send-email", email);
        await Task.Delay(500);

        var log = _fixture.GetLog();
        Assert.Contains("Email sent to user@example.com: Welcome", log);

        var command = await GetLastCommandAsync();
        Assert.True(command.IsCompleted);
        Assert.False(command.IsFailed);
    }

    [Fact]
    public async Task Example_LongRunningTaskWithTimeout()
    {
        var video = new ProcessVideoCommand(VideoId: "video123", ProcessingTimeMs: 5000);
        await _fixture.Orchestratum.Append("process-video", video, timeout: TimeSpan.FromSeconds(1));
        await Task.Delay(2000);

        var command = await GetLastCommandAsync();
        Assert.False(command.IsCompleted);
    }

    [Fact]
    public async Task Example_RetryOnFailure()
    {
        var apiCall = new ApiCallCommand(Endpoint: "https://api.example.com/data", ShouldFail: true);
        await _fixture.Orchestratum.Append("call-external-api", apiCall, retryCount: 2);
        await Task.Delay(1000);

        var log = _fixture.GetLog();
        var failedCalls = log.Count(l => l.Contains("API call failed"));
        Assert.Equal(3, failedCalls);

        var command = await GetLastCommandAsync();
        Assert.True(command.IsFailed);
        Assert.True(command.RetriesLeft <= 0);
    }

    [Fact]
    public async Task Example_SuccessAfterRetry()
    {
        var successfulCall = new ApiCallCommand(Endpoint: "https://api.example.com/data", ShouldFail: false);
        await _fixture.Orchestratum.Append("call-external-api", successfulCall, retryCount: 2);
        await Task.Delay(500);

        var log = _fixture.GetLog();
        Assert.Contains("API call success: https://api.example.com/data", log);
        Assert.Single(log);

        var command = await GetLastCommandAsync();
        Assert.True(command.IsCompleted);
        Assert.False(command.IsFailed);
        Assert.Equal(2, command.RetriesLeft);
    }

    [Fact]
    public async Task Example_TargetingSpecificInstance()
    {
        var email = new SendEmailCommand("admin@example.com", "Alert", "Important message");
        await _fixture.Orchestratum.Append("send-email", email, targetKey: "production-instance-1");
        await Task.Delay(500);

        var command = await GetLastCommandAsync();
        Assert.Equal("production-instance-1", command.Target);
        // Примечание: В тестовом окружении команды могут выполниться независимо от target
    }

    [Fact]
    public async Task Example_MultipleCommandsInSequence()
    {
        await _fixture.Orchestratum.Append("send-email", new SendEmailCommand("user1@example.com", "Subject1", "Body1"));
        await _fixture.Orchestratum.Append("send-email", new SendEmailCommand("user2@example.com", "Subject2", "Body2"));
        await _fixture.Orchestratum.Append("send-email", new SendEmailCommand("user3@example.com", "Subject3", "Body3"));
        await Task.Delay(800);

        var log = _fixture.GetLog();
        Assert.Equal(3, log.Count);
        Assert.Contains("Email sent to user1@example.com: Subject1", log);
        Assert.Contains("Email sent to user2@example.com: Subject2", log);
        Assert.Contains("Email sent to user3@example.com: Subject3", log);

        var commands = await GetAllCommandsAsync();
        Assert.All(commands.TakeLast(3), cmd => Assert.True(cmd.IsCompleted));
    }

    [Fact]
    public async Task Example_CustomTimeoutAndRetry()
    {
        var video = new ProcessVideoCommand("shortVideo", ProcessingTimeMs: 100);
        await _fixture.Orchestratum.Append("process-video", video, timeout: TimeSpan.FromSeconds(10), retryCount: 5);
        await Task.Delay(500);

        var command = await GetLastCommandAsync();
        Assert.True(command.IsCompleted);
        Assert.Equal(TimeSpan.FromSeconds(10), command.Timeout);
        
        var log = _fixture.GetLog();
        Assert.Contains("Video shortVideo processed", log);
    }

    private async Task<CommandDbo> GetLastCommandAsync()
    {
        using var context = new OrchestratumDbContext(_fixture.ContextOptions);
        return await context.Commands.OrderByDescending(c => c.Id).FirstAsync();
    }

    private async Task<List<CommandDbo>> GetAllCommandsAsync()
    {
        using var context = new OrchestratumDbContext(_fixture.ContextOptions);
        return await context.Commands.ToListAsync();
    }
}
