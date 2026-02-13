# Orchestratum

[![NuGet](https://img.shields.io/nuget/v/Orchestratum.svg)](https://www.nuget.org/packages/Orchestratum/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/PogovorovDaniil/Orchestratum)

A powerful and flexible command orchestration library for .NET applications with persistent storage, automatic retries, distributed execution support, and command chaining capabilities.

## ✨ Features

- **Command/Handler Pattern** - Clean separation of command definitions and execution logic
- **Persistent Command Queue** - Commands are stored in a database with full state tracking
- **Command Chaining** - Support for conditional command execution based on success, failure, or cancellation
- **Automatic Retries** - Configurable retry logic with automatic retry management
- **Timeout Management** - Per-command timeout configuration with automatic timeout detection
- **Distributed Execution** - Database-level locking for safe multi-instance deployments with target-based routing
- **Typed Commands** - Type-safe command definitions with input and output types
- **Flexible Registration** - Automatic command discovery or explicit registration
- **Background Processing** - Runs as a hosted service in ASP.NET Core or generic .NET hosts
- **Entity Framework Core Integration** - Works with any EF Core supported database

## 🚀 Quick Start

### Installation

```bash
dotnet add package Orchestratum
```

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

## 📖 Documentation

For detailed documentation, see:
- [Orchestratum Documentation](src/Orchestratum/README.md)

## 🌐 Language Versions

- [English](README.md)
- [Русский](README.ru.md)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🔗 Links

- [GitHub Repository](https://github.com/PogovorovDaniil/Orchestratum)
- [NuGet Package](https://www.nuget.org/packages/Orchestratum/)
