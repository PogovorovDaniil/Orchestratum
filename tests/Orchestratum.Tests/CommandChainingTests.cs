using Orchestratum.Tests.Commands;
using Orchestratum.Tests.Fixtures;
using Xunit;

namespace Orchestratum.Tests;

/// <summary>
/// Тесты цепочек команд (OnSuccess, OnFailure)
/// </summary>
public class CommandChainingTests : TestBase
{
    public CommandChainingTests(TestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CommandChaining_OnSuccess_ShouldExecuteFollowUpCommands()
    {
        // Arrange
        var command = new ProcessOrderCommand
        {
            Input = new OrderData("ORDER-123", 100m)
        };

        // Act
        await Fixture.Orchestratum.Push(command);
        await Task.Delay(1000);

        // Assert
        var log = GetLog();
        Assert.Contains("Processing order: ORDER-123", log);
        Assert.Contains("Email sent to customer@example.com: Order Confirmed", log);

        var commands = await GetAllCommandsAsync();
        var orderCommand = commands.FirstOrDefault(c => c.Name == "process_order");
        var emailCommand = commands.FirstOrDefault(c => c.Name == "send_email");

        Assert.NotNull(orderCommand);
        Assert.NotNull(emailCommand);
        Assert.True(orderCommand.IsCompleted);
        Assert.True(emailCommand.IsCompleted);
    }

    [Fact]
    public async Task CommandChaining_OnFailure_ShouldExecuteFailureCommands()
    {
        // Arrange
        var command = new ProcessOrderCommand
        {
            Input = new OrderData("ORDER-456", -1m) // Negative amount will fail
        };

        // Act
        await Fixture.Orchestratum.Push(command);
        await Task.Delay(1000);

        // Assert
        var log = GetLog();
        Assert.Contains("Processing order: ORDER-456", log);
        Assert.Contains("Email sent to admin@example.com: Order Failed", log);

        var commands = await GetAllCommandsAsync();
        var orderCommand = commands.FirstOrDefault(c => c.Name == "process_order");
        var emailCommand = commands.FirstOrDefault(c => c.Name == "send_email");

        Assert.NotNull(orderCommand);
        Assert.NotNull(emailCommand);
        Assert.True(orderCommand.IsFailed);
        Assert.True(emailCommand.IsCompleted);
    }
}
