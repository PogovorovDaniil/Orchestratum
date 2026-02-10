using Orchestratum.Tests.Commands;
using Orchestratum.Tests.Fixtures;
using Xunit;

namespace Orchestratum.Tests;

/// <summary>
/// Тесты повторов и таймаутов команд
/// </summary>
public class RetryAndTimeoutTests : TestBase
{
    public RetryAndTimeoutTests(TestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task LongRunningTaskWithTimeout_ShouldNotComplete()
    {
        // Arrange
        var command = new ProcessVideoCommand
        {
            Input = new VideoData("video123", 5000),
            Timeout = TimeSpan.FromSeconds(1)
        };

        // Act
        await Fixture.Orchestratum.Push(command);
        await Task.Delay(2000);

        // Assert
        var dbCommand = await GetLastCommandAsync();
        Assert.False(dbCommand.IsCompleted);
    }

    [Fact]
    public async Task RetryOnFailure_ShouldRetryExpectedTimes()
    {
        // Arrange
        var command = new CallExternalApiCommand
        {
            Input = new ApiData("https://api.example.com/data", true)
        };

        // Act
        await Fixture.Orchestratum.Push(command);
        await Task.Delay(1000);

        // Assert
        var log = GetLog();
        var failedCalls = log.Count(l => l.Contains("API call failed"));
        Assert.Equal(3, failedCalls); // Initial + 2 retries

        var dbCommand = await GetLastCommandAsync();
        Assert.True(dbCommand.IsFailed);
        Assert.True(dbCommand.RetriesLeft <= -1);
    }

    [Fact]
    public async Task SuccessAfterRetry_ShouldCompleteWithoutRetries()
    {
        // Arrange
        var command = new CallExternalApiCommand
        {
            Input = new ApiData("https://api.example.com/data", false)
        };

        // Act
        await Fixture.Orchestratum.Push(command);
        await Task.Delay(500);

        // Assert
        var log = GetLog();
        Assert.Contains("API call success: https://api.example.com/data", log);
        Assert.Single(log);

        var dbCommand = await GetLastCommandAsync();
        Assert.True(dbCommand.IsCompleted);
        Assert.False(dbCommand.IsFailed);
        Assert.Equal(2, dbCommand.RetriesLeft);
    }

    [Fact]
    public async Task CustomTimeoutAndRetry_ShouldUseCustomValues()
    {
        // Arrange
        var command = new ProcessVideoCommand
        {
            Input = new VideoData("shortVideo", 100),
            Timeout = TimeSpan.FromSeconds(10),
            RetryCount = 5
        };

        // Act
        await Fixture.Orchestratum.Push(command);
        await Task.Delay(500);

        // Assert
        var dbCommand = await GetLastCommandAsync();
        Assert.True(dbCommand.IsCompleted);
        Assert.Equal(TimeSpan.FromSeconds(10), dbCommand.Timeout);

        var log = GetLog();
        Assert.Contains("Video shortVideo processed", log);
    }
}
