using Orchestratum.Tests.Commands;
using Orchestratum.Tests.Fixtures;
using Xunit;

namespace Orchestratum.Tests;

/// <summary>
/// Тесты отложенного выполнения команд (Delay)
/// </summary>
public class DelayedExecutionTests : TestBase
{
    public DelayedExecutionTests(TestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task DelayedExecution_ShouldWaitBeforeExecution()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var command = new SendNotificationCommand
        {
            Input = new NotificationData("Delayed notification")
        };

        // Act
        await Fixture.Orchestratum.Push(command);

        // Wait less than delay time
        await Task.Delay(1000);
        var log = GetLog();
        Assert.Empty(log); // Command should not execute yet

        // Wait for delay to pass
        await Task.Delay(2000);
        log = GetLog();

        // Assert
        Assert.Contains("Notification sent: Delayed notification", log);

        var dbCommand = await GetLastCommandAsync();
        Assert.True(dbCommand.IsCompleted);
        Assert.True((dbCommand.ScheduledAt - startTime).TotalSeconds >= 2);
    }
}
