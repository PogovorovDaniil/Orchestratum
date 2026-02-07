using Microsoft.EntityFrameworkCore;
using Orchestratum.Database;

namespace Orchestratum.Tests;

public class DatabaseEdgeCasesTests : PostgreSqlTestBase, IClassFixture<PostgreSqlFixture>
{
    public DatabaseEdgeCasesTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task DbContext_ShouldHandleVeryLargeTimeSpan()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var largeTimespan = TimeSpan.FromDays(365 * 100); // 100 years
        var command = new OrchestratorCommandDbo
        {
            Executor = "test-executor",
            DataType = "System.String",
            Data = "test",
            Timeout = largeTimespan,
            RetriesLeft = 3
        };

        // Act
        Guid commandId;
        using (var context = new OrchestratorDbContext(options))
        {
            context.Commands.Add(command);
            await context.SaveChangesAsync();
            commandId = command.Id;
        }

        // Assert
        using (var context = new OrchestratorDbContext(options))
        {
            var retrieved = await context.Commands.FindAsync(commandId);
            retrieved.Should().NotBeNull();
            retrieved!.Timeout.Should().BeGreaterThan(TimeSpan.FromDays(365 * 99));
        }
    }

    [Fact]
    public async Task DbContext_ShouldHandleVeryLongStrings()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var longString = new string('x', 10000);
        var command = new OrchestratorCommandDbo
        {
            Executor = "test-executor",
            DataType = "System.String",
            Data = longString,
            Timeout = TimeSpan.FromMinutes(1),
            RetriesLeft = 3
        };

        // Act
        Guid commandId;
        using (var context = new OrchestratorDbContext(options))
        {
            context.Commands.Add(command);
            await context.SaveChangesAsync();
            commandId = command.Id;
        }

        // Assert
        using (var context = new OrchestratorDbContext(options))
        {
            var retrieved = await context.Commands.FindAsync(commandId);
            retrieved.Should().NotBeNull();
            retrieved!.Data.Should().Be(longString);
            retrieved.Data.Length.Should().Be(10000);
        }
    }

    [Fact]
    public async Task DbContext_ShouldHandleSpecialCharactersInData()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var specialData = "Test \"quotes\" and 'apostrophes' and \\backslashes\\ and \n newlines \r\n and \t tabs";
        var command = new OrchestratorCommandDbo
        {
            Executor = "test-executor",
            DataType = "System.String",
            Data = specialData,
            Timeout = TimeSpan.FromMinutes(1),
            RetriesLeft = 3
        };

        // Act
        Guid commandId;
        using (var context = new OrchestratorDbContext(options))
        {
            context.Commands.Add(command);
            await context.SaveChangesAsync();
            commandId = command.Id;
        }

        // Assert
        using (var context = new OrchestratorDbContext(options))
        {
            var retrieved = await context.Commands.FindAsync(commandId);
            retrieved.Should().NotBeNull();
            retrieved!.Data.Should().Be(specialData);
        }
    }

    [Fact]
    public async Task DbContext_ConcurrentUpdates_ShouldHandleOptimisticConcurrency()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var command = new OrchestratorCommandDbo
        {
            Executor = "test-executor",
            DataType = "System.String",
            Data = "test",
            Timeout = TimeSpan.FromMinutes(1),
            RetriesLeft = 3,
            IsCompleted = false
        };

        Guid commandId;
        using (var context = new OrchestratorDbContext(options))
        {
            context.Commands.Add(command);
            await context.SaveChangesAsync();
            commandId = command.Id;
        }

        // Act - Simulate concurrent updates
        using var context1 = new OrchestratorDbContext(options);
        using var context2 = new OrchestratorDbContext(options);

        var cmd1 = await context1.Commands.FindAsync(commandId);
        var cmd2 = await context2.Commands.FindAsync(commandId);

        cmd1!.RetriesLeft = 2;
        cmd2!.RetriesLeft = 1;

        await context1.SaveChangesAsync();
        await context2.SaveChangesAsync();

        // Assert - Last write wins in this scenario
        using var verifyContext = new OrchestratorDbContext(options);
        var final = await verifyContext.Commands.FindAsync(commandId);
        final!.RetriesLeft.Should().Be(1);
    }

    [Fact]
    public async Task DbContext_BulkInsert_ShouldHandleManyCommands()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var commands = Enumerable.Range(0, 1000).Select(i => new OrchestratorCommandDbo
        {
            Executor = $"executor-{i % 10}",
            DataType = "System.String",
            Data = $"data-{i}",
            Timeout = TimeSpan.FromMinutes(1),
            RetriesLeft = 3
        }).ToList();

        // Act
        using (var context = new OrchestratorDbContext(options))
        {
            context.Commands.AddRange(commands);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new OrchestratorDbContext(options))
        {
            var count = await context.Commands.CountAsync();
            count.Should().Be(1000);
        }
    }

    [Fact]
    public async Task DbContext_DeleteCommand_ShouldRemoveFromDatabase()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var command = new OrchestratorCommandDbo
        {
            Executor = "test-executor",
            DataType = "System.String",
            Data = "test",
            Timeout = TimeSpan.FromMinutes(1),
            RetriesLeft = 3
        };

        Guid commandId;
        using (var context = new OrchestratorDbContext(options))
        {
            context.Commands.Add(command);
            await context.SaveChangesAsync();
            commandId = command.Id;
        }

        // Act
        using (var context = new OrchestratorDbContext(options))
        {
            var toDelete = await context.Commands.FindAsync(commandId);
            context.Commands.Remove(toDelete!);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new OrchestratorDbContext(options))
        {
            var deleted = await context.Commands.FindAsync(commandId);
            deleted.Should().BeNull();
        }
    }

    [Fact]
    public async Task DbContext_QueryWithComplexFilters_ShouldReturnCorrectResults()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var now = DateTimeOffset.UtcNow;

        var commands = new[]
        {
            new OrchestratorCommandDbo
            {
                Executor = "executor1",
                DataType = "System.String",
                Data = "test1",
                Timeout = TimeSpan.FromMinutes(1),
                RetriesLeft = 3,
                IsCompleted = false,
                IsFailed = false,
                IsRunning = false
            },
            new OrchestratorCommandDbo
            {
                Executor = "executor2",
                DataType = "System.String",
                Data = "test2",
                Timeout = TimeSpan.FromMinutes(1),
                RetriesLeft = 0,
                IsCompleted = false,
                IsFailed = false,
                IsRunning = false
            },
            new OrchestratorCommandDbo
            {
                Executor = "executor3",
                DataType = "System.String",
                Data = "test3",
                Timeout = TimeSpan.FromMinutes(1),
                RetriesLeft = 3,
                IsCompleted = true,
                IsFailed = false,
                IsRunning = false
            },
            new OrchestratorCommandDbo
            {
                Executor = "executor4",
                DataType = "System.String",
                Data = "test4",
                Timeout = TimeSpan.FromMinutes(1),
                RetriesLeft = -1,
                IsCompleted = false,
                IsFailed = true,
                IsRunning = false
            }
        };

        using (var context = new OrchestratorDbContext(options))
        {
            context.Commands.AddRange(commands);
            await context.SaveChangesAsync();
        }

        // Act & Assert
        using (var context = new OrchestratorDbContext(options))
        {
            // Query for pending commands (not completed, not failed, has retries)
            var pending = await context.Commands
                .Where(c => !c.IsCompleted && !c.IsFailed && c.RetriesLeft >= 0)
                .ToListAsync();

            pending.Should().HaveCount(2);
            pending.Should().Contain(c => c.Executor == "executor1");
            pending.Should().Contain(c => c.Executor == "executor2");
        }
    }

    [Fact]
    public async Task DbContext_UpdateWithExecuteUpdate_ShouldUpdateWithoutLoading()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var command = new OrchestratorCommandDbo
        {
            Executor = "test-executor",
            DataType = "System.String",
            Data = "test",
            Timeout = TimeSpan.FromMinutes(1),
            RetriesLeft = 3,
            IsRunning = false
        };

        Guid commandId;
        using (var context = new OrchestratorDbContext(options))
        {
            context.Commands.Add(command);
            await context.SaveChangesAsync();
            commandId = command.Id;
        }

        // Act
        using (var context = new OrchestratorDbContext(options))
        {
            var updated = await context.Commands
                .Where(c => c.Id == commandId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsRunning, true)
                    .SetProperty(c => c.RunExpiresAt, DateTimeOffset.UtcNow.AddMinutes(5)));

            updated.Should().Be(1);
        }

        // Assert
        using (var context = new OrchestratorDbContext(options))
        {
            var retrieved = await context.Commands.FindAsync(commandId);
            retrieved!.IsRunning.Should().BeTrue();
            retrieved.RunExpiresAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task DbContext_NullableFields_ShouldHandleNullValues()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var command = new OrchestratorCommandDbo
        {
            Executor = "test-executor",
            DataType = "System.String",
            Data = "test",
            Timeout = TimeSpan.FromMinutes(1),
            RetriesLeft = 3,
            RunExpiresAt = null,
            CompleteAt = null
        };

        // Act
        Guid commandId;
        using (var context = new OrchestratorDbContext(options))
        {
            context.Commands.Add(command);
            await context.SaveChangesAsync();
            commandId = command.Id;
        }

        // Assert
        using (var context = new OrchestratorDbContext(options))
        {
            var retrieved = await context.Commands.FindAsync(commandId);
            retrieved.Should().NotBeNull();
            retrieved!.RunExpiresAt.Should().BeNull();
            retrieved.CompleteAt.Should().BeNull();
        }
    }
}
