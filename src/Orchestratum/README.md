# Orchestratum

[![NuGet](https://img.shields.io/nuget/v/Orchestratum.svg)](https://www.nuget.org/packages/Orchestratum/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/PogovorovDaniil/Orchestratum)

*[Русская версия](README.ru.md)*

A powerful and flexible command orchestration library for .NET applications with persistent storage, automatic retries, distributed execution support, and command chaining capabilities.

## Features

- **Command/Handler Pattern**: Clean separation of command definitions and execution logic
- **Persistent Command Queue**: Commands are stored in a database with full state tracking
- **Command Chaining**: Support for conditional command execution based on success, failure, or cancellation
- **Automatic Retries**: Configurable retry logic with automatic retry management
- **Timeout Management**: Per-command timeout configuration with automatic timeout detection
- **Distributed Execution**: Database-level locking for safe multi-instance deployments with target-based routing
- **Typed Commands**: Type-safe command definitions with input and output types
- **Flexible Registration**: Automatic command discovery or explicit registration
- **Background Processing**: Runs as a hosted service in ASP.NET Core or generic .NET hosts
- **Entity Framework Core Integration**: Works with any EF Core supported database

## Installation

```bash
dotnet add package Orchestratum
```

## Architecture Overview

Orchestratum is built around a command/handler pattern:

- **Commands** (`IOrchCommand`): Define what needs to be executed, including input data, timeout, retry count, and target instance
- **Handlers** (`IOrchCommandHandler<TCommand>`): Implement the actual execution logic for commands
- **Orchestrator** (`IOrchestratum`): Manages command enqueueing and orchestration

## Quick Start

### 1. Define a Command

```csharp
using Orchestratum.Contract;

// Command with input only
public class SendEmailCommand : OrchCommand<EmailData>
{
    public override TimeSpan Timeout => TimeSpan.FromMinutes(2);
    public override int RetryCount => 5;
}

// Command with input and output
[OrchCommand("generate_report")]
public class GenerateReportCommand : OrchCommand<ReportRequest, ReportResult>
{
    public override TimeSpan Timeout => TimeSpan.FromMinutes(10);

    // Chain another command on success
    protected override IEnumerable<IOrchCommand> OnSuccess(ReportResult output)
    {
        yield return new SendEmailCommand 
        { 
            Input = new EmailData 
            { 
                To = "admin@example.com",
                Subject = "Report Generated",
                Body = $"Report {output.ReportId} was generated"
            }
        };
    }
}
```

### 2. Implement Command Handler

```csharp
using Orchestratum.Contract;

public class SendEmailCommandHandler : IOrchCommandHandler<SendEmailCommand>
{
    private readonly IEmailService _emailService;

    public SendEmailCommandHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task<IOrchResult<SendEmailCommand>> Execute(
        SendEmailCommand command, 
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(command.Input, cancellationToken);
            return command.CreateResult(OrchResultStatus.Success);
        }
        catch (Exception)
        {
            return command.CreateResult(OrchResultStatus.Failed);
        }
    }
}

public class GenerateReportCommandHandler : IOrchCommandHandler<GenerateReportCommand>
{
    private readonly IReportService _reportService;

    public GenerateReportCommandHandler(IReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task<IOrchResult<GenerateReportCommand>> Execute(
        GenerateReportCommand command, 
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GenerateAsync(command.Input, cancellationToken);
        return command.CreateResult(result, OrchResultStatus.Success);
    }
}
```

### 3. Configure Services

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices(services =>
{
    // Register application services
    services.AddSingleton<IEmailService, EmailService>();
    services.AddSingleton<IReportService, ReportService>();

    // Configure Orchestratum
    services.AddOchestratum(opts =>
    {
        // Configure database
        opts.ConfigureDbContext(db => 
            db.UseNpgsql("Host=localhost;Database=myapp"));

        // Register commands and handlers from assemblies
        opts.RegisterCommands(typeof(Program).Assembly);
        opts.RegisterHandlers(typeof(Program).Assembly);

        // Configure options
        opts.CommandPollingInterval = TimeSpan.FromSeconds(5);
        opts.LockTimeoutBuffer = TimeSpan.FromSeconds(10);
        opts.MaxCommandPull = 100;
        opts.InstanceKey = "default"; // For distributed scenarios
        opts.TablePrefix = "ORCH_"; // Database table prefix
    });
});

builder.Build().Run();
```

### 4. Enqueue Commands

```csharp
public class ReportController : ControllerBase
{
    private readonly IOrchestratum _orchestratum;

    public ReportController(IOrchestratum orchestratum)
    {
        _orchestratum = orchestratum;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateReport(ReportRequest request)
    {
        var command = new GenerateReportCommand
        {
            Input = request
        };

        await _orchestratum.Push(command);

        return Accepted(new { commandId = command.Id });
    }
}
```

## Configuration Options

### OrchServiceConfiguration Properties

```csharp
services.AddOchestratum(opts =>
{
    // Database configuration (required)
    opts.ConfigureDbContext(db => db.UseNpgsql(connectionString));

    // Polling interval for checking new commands (default: 5 seconds)
    opts.CommandPollingInterval = TimeSpan.FromSeconds(5);

    // Buffer time added to command timeout for lock expiration (default: 10 seconds)
    opts.LockTimeoutBuffer = TimeSpan.FromSeconds(10);

    // Maximum number of commands to pull in one polling cycle (default: 100)
    opts.MaxCommandPull = 100;

    // Instance key for distributed scenarios (default: "default")
    opts.InstanceKey = "worker-1";

    // Database table prefix (default: "ORCH_")
    opts.TablePrefix = "ORCHESTRATUM_";
});
```

## Database Setup

### Supported Databases

Any database supported by Entity Framework Core:
- PostgreSQL (recommended)
- SQL Server
- MySQL / MariaDB
- Oracle
- And more...

### Creating Migrations

Create a design-time factory in your project:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Orchestratum.Database;

namespace YourProject.Database;

public class OrchDbContextFactory : IDesignTimeDbContextFactory<OrchDbContext>
{
    public OrchDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrchDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=myapp;Username=user;Password=pass", 
            opts => opts.MigrationsAssembly(typeof(OrchDbContextFactory).Assembly.GetName().Name));

        return new OrchDbContext(optionsBuilder.Options, "ORCH_");
    }
}
```

Run migration commands:

```bash
# Add migration
dotnet ef migrations add InitialOrchestratum --context OrchDbContext

# Apply migration
dotnet ef database update --context OrchDbContext

# Remove last migration (if needed)
dotnet ef migrations remove --context OrchDbContext
```

### Database Schema

Commands are stored in the `{prefix}commands` table (default: `ORCH_commands`):

| Column | Type | Description |
|--------|------|-------------|
| `id` | GUID | Unique command identifier |
| `target` | string | Target instance key for routing |
| `name` | string | Command name (from attribute or convention) |
| `input` | string | JSON-serialized input data |
| `output` | string | JSON-serialized output data |
| `scheduled_at` | DateTimeOffset | When command should execute |
| `timeout` | TimeSpan | Maximum execution duration |
| `is_running` | bool | Whether command is currently executing |
| `running_at` | DateTimeOffset? | When execution started |
| `run_expires_at` | DateTimeOffset? | When execution lock expires |
| `is_completed` | bool | Whether command completed successfully |
| `completed_at` | DateTimeOffset? | When command completed |
| `is_canceled` | bool | Whether command was canceled |
| `canceled_at` | DateTimeOffset? | When command was canceled |
| `retries_left` | int | Remaining retry attempts |
| `is_failed` | bool | Whether command failed permanently |
| `failed_at` | DateTimeOffset? | When command failed |

## Advanced Features

### Command Naming

Commands are automatically named based on class name:

```csharp
// Automatic naming: "send_email"
public class SendEmailCommand : OrchCommand<EmailData> { }

// Explicit naming via attribute
[OrchCommand("email.send")]
public class SendEmailCommand : OrchCommand<EmailData> { }
```

Naming convention:
1. Removes "Command" suffix
2. Converts PascalCase to snake_case
3. Uses lowercase

### Command Chaining

Chain commands based on execution result:

```csharp
public class ProcessOrderCommand : OrchCommand<OrderData, OrderResult>
{
    // Execute these commands on success
    protected override IEnumerable<IOrchCommand> OnSuccess(OrderResult output)
    {
        yield return new SendConfirmationEmailCommand 
        { 
            Input = new EmailData { OrderId = output.OrderId }
        };

        yield return new UpdateInventoryCommand
        {
            Input = new InventoryUpdate { Items = output.Items }
        };
    }

    // Execute these commands on failure
    protected override IEnumerable<IOrchCommand> OnFailure()
    {
        yield return new NotifyAdminCommand
        {
            Input = new AdminNotification { OrderId = Id }
        };
    }

    // Execute these commands on cancellation
    protected override IEnumerable<IOrchCommand> OnCancellation()
    {
        yield return new RefundPaymentCommand
        {
            Input = new PaymentRefund { OrderId = Id }
        };
    }
}
```

### Distributed Execution

Route commands to specific instances using the `Target` property:

```csharp
// Configure instances
services.AddOchestratum(opts =>
{
    opts.InstanceKey = "email-worker"; // This instance processes email commands
    // ...
});

// Route command to specific instance
var command = new SendEmailCommand
{
    Input = emailData,
    Target = "email-worker" // Will only be processed by email-worker instance
};

await _orchestratum.Push(command);
```

### Retry Behavior

Retries are automatic:
- Command fails → `RetriesLeft` decremented
- If `RetriesLeft >= 0` → Command becomes available for retry
- If `RetriesLeft == -1` → Command marked as permanently failed
- `OnFailure` commands are enqueued only after final failure

### Timeout Handling

Timeouts are enforced automatically:
- Lock is refreshed periodically during execution
- If execution exceeds timeout → command is canceled
- Timeout triggers retry (if retries available)
- Lock expiration allows stale commands to be re-picked

### Result Status

Commands return status via `IOrchResult`:

```csharp
public enum OrchResultStatus
{
    Success,    // Command executed successfully
    Cancelled,  // Command was cancelled (timeout or explicit)
    Failed,     // Command failed (exception or explicit)
    NotFound,   // Handler not found
    TimedOut    // Command exceeded timeout
}
```

### Explicit Registration

Register commands and handlers explicitly:

```csharp
services.AddOchestratum(opts =>
{
    // Register specific command
    opts.RegisterCommand(typeof(SendEmailCommand));

    // Register specific handler
    opts.RegisterHandler<SendEmailCommandHandler>();

    // Or register from assemblies
    opts.RegisterCommands(Assembly.GetExecutingAssembly());
    opts.RegisterHandlers(Assembly.GetExecutingAssembly());
});
```

## Best Practices

1. **Command Design**: Keep commands small and focused on a single responsibility
2. **Idempotency**: Design handlers to be idempotent since commands may be retried
3. **Timeout Configuration**: Set realistic timeouts based on expected execution time
4. **Retry Strategy**: Use retries for transient failures, not business logic errors
5. **Target Routing**: Use targets to scale specific command types independently
6. **Database Choice**: Use PostgreSQL or SQL Server for production scenarios
7. **Monitoring**: Track command states in the database for observability

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
