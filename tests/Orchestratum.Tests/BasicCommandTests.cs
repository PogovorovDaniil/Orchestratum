using Orchestratum.Tests.Commands;
using Orchestratum.Tests.Fixtures;
using Xunit;

namespace Orchestratum.Tests;

/// <summary>
/// Базовые тесты выполнения команд
/// </summary>
public class BasicCommandTests : TestBase
{
    public BasicCommandTests(TestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SimpleEmailSending_ShouldExecuteSuccessfully()
    {
        // Arrange
        var command = new SendEmailCommand
        {
            Input = new EmailData("user@example.com", "Welcome", "Welcome to our service!")
        };

        // Act
        await Fixture.Orchestratum.Push(command);
        await Task.Delay(500);

        // Assert
        var log = GetLog();
        Assert.Contains("Email sent to user@example.com: Welcome", log);

        var dbCommand = await GetLastCommandAsync();
        Assert.True(dbCommand.IsCompleted);
        Assert.False(dbCommand.IsFailed);
    }

    [Fact]
    public async Task CommandIdIsPopulated_AfterPush()
    {
        // Arrange
        var command = new SendEmailCommand
        {
            Input = new EmailData("test@example.com", "Test", "Body")
        };

        // Act
        await Fixture.Orchestratum.Push(command);

        // Assert
        Assert.NotEqual(Guid.Empty, command.Id); // Id is populated after Push

        var dbCommand = await GetCommandByIdAsync(command.Id);
        Assert.NotNull(dbCommand);
        Assert.Equal(command.Id, dbCommand.Id);
    }

    [Fact]
    public async Task TargetingSpecificInstance_ShouldSetTargetCorrectly()
    {
        // Arrange
        var command = new SendEmailCommand
        {
            Input = new EmailData("admin@example.com", "Alert", "Important message"),
            Target = "production-instance-1"
        };

        // Act
        await Fixture.Orchestratum.Push(command);
        await Task.Delay(500);

        // Assert
        var dbCommand = await GetLastCommandAsync();
        Assert.Equal("production-instance-1", dbCommand.Target);
    }

    [Fact]
    public async Task MultipleCommandsInSequence_ShouldExecuteAll()
    {
        // Arrange & Act
        await Fixture.Orchestratum.Push(new SendEmailCommand { Input = new EmailData("user1@example.com", "Subject1", "Body1") });
        await Fixture.Orchestratum.Push(new SendEmailCommand { Input = new EmailData("user2@example.com", "Subject2", "Body2") });
        await Fixture.Orchestratum.Push(new SendEmailCommand { Input = new EmailData("user3@example.com", "Subject3", "Body3") });
        await Task.Delay(800);

        // Assert
        var log = GetLog();
        Assert.Equal(3, log.Count);
        Assert.Contains("Email sent to user1@example.com: Subject1", log);
        Assert.Contains("Email sent to user2@example.com: Subject2", log);
        Assert.Contains("Email sent to user3@example.com: Subject3", log);

        var commands = await GetAllCommandsAsync();
        Assert.All(commands.TakeLast(3), cmd => Assert.True(cmd.IsCompleted));
    }

    [Fact]
    public async Task ConcurrentCommandExecution_ShouldHandleMultipleCommands()
    {
        // Arrange
        var commands = Enumerable.Range(1, 5)
            .Select(i => new SendEmailCommand
            {
                Input = new EmailData($"user{i}@example.com", $"Subject{i}", "Body")
            })
            .ToList();

        // Act
        foreach (var command in commands)
        {
            await Fixture.Orchestratum.Push(command);
        }
        await Task.Delay(1000);

        // Assert
        var log = GetLog();
        Assert.Equal(5, log.Count);

        var dbCommands = await GetAllCommandsAsync();
        Assert.All(dbCommands.TakeLast(5), cmd => Assert.True(cmd.IsCompleted));
    }
}
