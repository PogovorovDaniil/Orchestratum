# Orchestratum

[![NuGet](https://img.shields.io/nuget/v/Orchestratum.svg)](https://www.nuget.org/packages/Orchestratum/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/PogovorovDaniil/Orchestratum)

*[Русская версия](README.ru.md)*

A lightweight, persistent background task orchestrator for .NET applications with built-in retry logic, timeouts, and database persistence.

## Features

- **Persistent Task Queue**: Tasks are stored in a database, ensuring reliability across application restarts
- **Automatic Retries**: Configurable retry logic for failed tasks
- **Timeout Management**: Set execution timeouts for individual tasks or use defaults
- **Distributed Lock**: Prevents duplicate task execution in multi-instance deployments
- **Flexible Executor System**: Register custom executors for different task types
- **Background Processing**: Runs as a hosted service in ASP.NET Core or generic .NET hosts
- **Entity Framework Core Integration**: Works with any EF Core supported database

## Installation

```bash
dotnet add package Orchestratum
```

## Quick Start

### 1. Configure Services

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices(services =>
{
    services.AddOchestrator((sp, opts) => opts
        .ConfigureDbContext(opts => 
            opts.UseNpgsql("Host=localhost;Database=myapp"))
        .RegisterExecutor("my-task", async (serviceProvider, data, cancellationToken) =>
        {
            // Your task logic here
            var myData = (MyTaskData)data;
            await ProcessTask(myData);
        }));
});

builder.Build().Run();
```

### 2. Enqueue Tasks

```csharp
public class MyService
{
    private readonly IOrchestrator _orchestrator;

    public MyService(IOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task EnqueueWork()
    {
        // Enqueue a task with default timeout and retry settings
        await _orchestrator.Append("my-task", new MyTaskData { Value = "Hello" });

        // Enqueue with custom timeout and retry count
        await _orchestrator.Append(
            "my-task", 
            new MyTaskData { Value = "World" },
            timeout: TimeSpan.FromMinutes(5),
            retryCount: 5
        );
    }
}
```

## Configuration Options

The orchestrator provides several configuration options:

```csharp
services.AddOchestrator((sp, opts) => opts
    .ConfigureDbContext(opts => opts.UseNpgsql(connectionString))
    .RegisterExecutor("executor-key", executorDelegate)
    .With(o =>
    {
        // Polling interval for checking new commands (default: 1 minute)
        o.CommandPollingInterval = TimeSpan.FromSeconds(30);

        // Buffer time for lock timeout (default: 1 second)
        o.LockTimeoutBuffer = TimeSpan.FromSeconds(2);

        // Default timeout for tasks (default: 1 minute)
        o.DefaultTimeout = TimeSpan.FromMinutes(5);

        // Default retry count (default: 3)
        o.DefaultRetryCount = 5;
    }));
```

## Database Setup

Orchestratum uses Entity Framework Core for data persistence. You need to create the required tables in your database.

### Supported Databases

Any database supported by Entity Framework Core can be used:
- PostgreSQL (recommended)
- SQL Server
- MySQL
- SQLite
- And more...

### Creating Migrations

Since `OrchestratorDbContext` is in the library, you need to create a design-time factory in your main project to enable migrations:

**Step 1:** Create a factory class in your project:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Orchestratum.Database;

namespace YourProject.Database;

public class OrchestratorDbContextFactory : IDesignTimeDbContextFactory<OrchestratorDbContext>
{
    public OrchestratorDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrchestratorDbContext>();
        
        // Configure your database provider
        optionsBuilder.UseNpgsql("Host=localhost;Database=myapp;Username=user;Password=pass", 
            opts => opts.MigrationsAssembly(typeof(OrchestratorDbContextFactory).Assembly.GetName().Name));

        return new OrchestratorDbContext(optionsBuilder.Options);
    }
}
```

**Step 2:** Run migration commands:

```bash
# Add migration
dotnet ef migrations add InitialOrchestrator --context OrchestratorDbContext

# Apply migration
dotnet ef database update --context OrchestratorDbContext

# Remove last migration (if needed)
dotnet ef migrations remove --context OrchestratorDbContext
```

### Database Schema

The orchestrator stores commands in a table named `orchestrator_commands` with the following columns:
- `id` - Unique command identifier (GUID)
- `executor` - Executor key
- `data_type` - Serialized data type
- `data` - JSON serialized command data
- `timeout` - Execution timeout
- `retries_left` - Remaining retry attempts
- `is_running` - Execution status flag
- `is_completed` - Completion status flag
- `is_failed` - Failure status flag
- `locked_until` - Lock expiration timestamp

## Advanced Usage

### Custom Executors

You can register multiple executors for different task types:

```csharp
services.AddOchestrator((sp, opts) => opts
    .ConfigureDbContext(opts => opts.UseNpgsql(connectionString))
    .RegisterExecutor("send-email", async (sp, data, ct) =>
    {
        var emailService = sp.GetRequiredService<IEmailService>();
        var emailData = (EmailData)data;
        await emailService.SendAsync(emailData, ct);
    })
    .RegisterExecutor("generate-report", async (sp, data, ct) =>
    {
        var reportService = sp.GetRequiredService<IReportService>();
        var reportData = (ReportData)data;
        await reportService.GenerateAsync(reportData, ct);
    }));
```

### Error Handling

Failed tasks are automatically retried based on the configured retry count. After all retries are exhausted, the task is marked as failed and won't be retried again.

```csharp
// This task will be retried 5 times if it fails
await _orchestrator.Append("my-task", data, retryCount: 5);
```

### Timeout Handling

Each task can have its own timeout. If a task exceeds the timeout, it will be marked as failed and retried (if retries are available).

```csharp
// This task will timeout after 10 minutes
await _orchestrator.Append("long-task", data, timeout: TimeSpan.FromMinutes(10));
```

### Distributed Scenarios

Orchestratum uses database-level locking to prevent the same task from being executed multiple times in distributed scenarios. This is especially useful when running multiple instances of your application.

## Extensions

- **[Orchestratum.MediatR](../Orchestratum.MediatR/README.md)** - MediatR integration for command/query patterns

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
