using Orchestratum.Database;

namespace Orchestratum.Tests;

public class OrchestratumCommandDboTests : PostgreSqlTestBase, IClassFixture<PostgreSqlFixture>
{
    public OrchestratumCommandDboTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void Constructor_ShouldGenerateNewGuid()
    {
        // Act
        var command1 = new CommandDbo
        {
            Executor = "test",
            Target = "default",
            DataType = "string",
            Data = "test"
        };
        var command2 = new CommandDbo
        {
            Executor = "test",
            Target = "default",
            DataType = "string",
            Data = "test"
        };

        // Assert
        command1.Id.Should().NotBe(Guid.Empty);
        command2.Id.Should().NotBe(Guid.Empty);
        command1.Id.Should().NotBe(command2.Id);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var id = Guid.NewGuid();
        var executor = "test-executor";
        var dataType = "System.String";
        var data = "test data";
        var timeout = TimeSpan.FromMinutes(5);
        var retriesLeft = 3;
        var runExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var completeAt = DateTimeOffset.UtcNow;

        // Act
        var command = new CommandDbo
        {
            Id = id,
            Executor = executor,
            Target = "default",
            DataType = dataType,
            Data = data,
            Timeout = timeout,
            RetriesLeft = retriesLeft,
            IsRunning = true,
            RunExpiresAt = runExpiresAt,
            IsCompleted = true,
            CompleteAt = completeAt,
            IsFailed = false
        };

        // Assert
        command.Id.Should().Be(id);
        command.Executor.Should().Be(executor);
        command.DataType.Should().Be(dataType);
        command.Data.Should().Be(data);
        command.Timeout.Should().Be(timeout);
        command.RetriesLeft.Should().Be(retriesLeft);
        command.IsRunning.Should().BeTrue();
        command.RunExpiresAt.Should().Be(runExpiresAt);
        command.IsCompleted.Should().BeTrue();
        command.CompleteAt.Should().Be(completeAt);
        command.IsFailed.Should().BeFalse();
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Act
        var command = new CommandDbo
        {
            Executor = "test",
            Target = "default",
            DataType = "string",
            Data = "test"
        };

        // Assert
        command.IsRunning.Should().BeFalse();
        command.RunExpiresAt.Should().BeNull();
        command.IsCompleted.Should().BeFalse();
        command.CompleteAt.Should().BeNull();
        command.IsFailed.Should().BeFalse();
        command.RetriesLeft.Should().Be(0);
        command.Timeout.Should().Be(TimeSpan.Zero);
    }
}

public class OrchestratumDbContextTests : PostgreSqlTestBase, IClassFixture<PostgreSqlFixture>
{
    public OrchestratumDbContextTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }
    [Fact]
    public void DbContext_ShouldHaveCommandsDbSet()
    {
        // Arrange
        var options = CreateDbContextOptions();

        // Act
        using var context = new OrchestratumDbContext(options);

        // Assert
        context.Commands.Should().NotBeNull();
    }

    [Fact]
    public async Task DbContext_ShouldPersistCommands()
    {
        // Arrange
        var options = CreateDbContextOptions();

        var command = new CommandDbo
        {
            Executor = "test-executor",
            Target = "default",
            DataType = "System.String",
            Data = "test data",
            Timeout = TimeSpan.FromMinutes(1),
            RetriesLeft = 3
        };

        // Act
        Guid commandId;
        using (var context = new OrchestratumDbContext(options))
        {
            context.Commands.Add(command);
            await context.SaveChangesAsync();
            commandId = command.Id;
        }

        // Assert
        using (var context = new OrchestratumDbContext(options))
        {
            var retrievedCommand = await context.Commands.FindAsync(commandId);
            retrievedCommand.Should().NotBeNull();
            retrievedCommand!.Executor.Should().Be("test-executor");
            retrievedCommand.DataType.Should().Be("System.String");
            retrievedCommand.Data.Should().Be("test data");
        }
    }

    [Fact]
    public async Task DbContext_ShouldUpdateCommands()
    {
        // Arrange
        var options = CreateDbContextOptions();

        var command = new CommandDbo
        {
            Executor = "test-executor",
            Target = "default",
            DataType = "System.String",
            Data = "test data",
            Timeout = TimeSpan.FromMinutes(1),
            RetriesLeft = 3,
            IsCompleted = false
        };

        Guid commandId;
        using (var context = new OrchestratumDbContext(options))
        {
            context.Commands.Add(command);
            await context.SaveChangesAsync();
            commandId = command.Id;
        }

        // Act
        using (var context = new OrchestratumDbContext(options))
        {
            var commandToUpdate = await context.Commands.FindAsync(commandId);
            commandToUpdate!.IsCompleted = true;
            commandToUpdate.CompleteAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new OrchestratumDbContext(options))
        {
            var updatedCommand = await context.Commands.FindAsync(commandId);
            updatedCommand!.IsCompleted.Should().BeTrue();
            updatedCommand.CompleteAt.Should().NotBeNull();
        }
    }
}
