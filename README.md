# Orchestratum

[![NuGet](https://img.shields.io/nuget/v/Orchestratum.svg)](https://www.nuget.org/packages/Orchestratum/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)

A lightweight, persistent background task orchestrator for .NET applications with built-in retry logic, timeouts, and database persistence using Entity Framework Core.

## 📦 Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| **Orchestratum** | Core orchestration library | [![NuGet](https://img.shields.io/nuget/v/Orchestratum.svg)](https://www.nuget.org/packages/Orchestratum/) |
| **Orchestratum.MediatR** | MediatR integration | [![NuGet](https://img.shields.io/nuget/v/Orchestratum.MediatR.svg)](https://www.nuget.org/packages/Orchestratum.MediatR/) |

## ✨ Features

- **Persistent Task Queue** - Tasks stored in database, survive application restarts
- **Automatic Retries** - Configurable retry logic for failed tasks
- **Timeout Management** - Per-task or default execution timeouts
- **Distributed Lock** - Prevents duplicate execution in multi-instance deployments
- **Flexible Executors** - Register custom executors for different task types
- **Background Processing** - Runs as hosted service
- **EF Core Integration** - Works with any EF Core supported database

## 🚀 Quick Start

### Installation

```bash
dotnet add package Orchestratum
# Or with MediatR integration
dotnet add package Orchestratum.MediatR
```

### Basic Usage (without MediatR)

```csharp
// 1. Configure services
services.AddOchestrator((sp, opts) =>
{
    opts.ConfigureDbContext(dbOpts => 
        dbOpts.UseNpgsql("Host=localhost;Database=myapp"));

    opts.RegisterExecutor("send-email", async (serviceProvider, data, cancellationToken) =>
    {
        var emailService = serviceProvider.GetRequiredService<IEmailService>();
        var emailData = (EmailData)data;
        await emailService.SendAsync(emailData, cancellationToken);
    });

    // Configure options
    opts.DefaultTimeout = TimeSpan.FromMinutes(5);
    opts.DefaultRetryCount = 3;
    opts.CommandPollingInterval = TimeSpan.FromSeconds(30);
});

// 2. Inject IOrchestrator and enqueue tasks
public class MyService
{
    private readonly IOrchestrator _orchestrator;

    public MyService(IOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task SendEmail(string to, string subject, string body)
    {
        // Enqueue with default settings
        await _orchestrator.Append("send-email", new EmailData 
        { 
            To = to, 
            Subject = subject, 
            Body = body 
        });

        // Or with custom timeout and retry count
        await _orchestrator.Append(
            "send-email", 
            new EmailData { To = to, Subject = subject, Body = body },
            timeout: TimeSpan.FromMinutes(10),
            retryCount: 5
        );
    }
}
```

### Usage with MediatR

```csharp
// 1. Configure services
services.AddMediatR(opts => 
    opts.RegisterServicesFromAssembly(typeof(Program).Assembly));

services.AddOchestrator((sp, opts) =>
{
    opts.ConfigureDbContext(dbOpts => dbOpts.UseNpgsql("Host=localhost;Database=myapp"));
    opts.RegisterMediatR();  // Enable MediatR integration
});

// 2. Define MediatR request and handler
public record SendEmailCommand(string To, string Subject, string Body) : IRequest;

public class SendEmailHandler : IRequestHandler<SendEmailCommand>
{
    private readonly IEmailService _emailService;

    public SendEmailHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        await _emailService.SendAsync(request.To, request.Subject, request.Body, cancellationToken);
    }
}

// 3. Queue MediatR requests
public class MyService
{
    private readonly IOrchestrator _orchestrator;

    public MyService(IOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public void SendEmail(string to, string subject, string body)
    {
        // Simply append MediatR request
        _orchestrator.Append(new SendEmailCommand(to, subject, body));

        // Or with custom settings
        _orchestrator.Append(
            new SendEmailCommand(to, subject, body),
            timeout: TimeSpan.FromMinutes(10),
            retryCount: 5
        );
    }
}
```

## 📖 Documentation

For detailed documentation, see:
- [Orchestratum Core Documentation](src/Orchestratum/README.md)
- [Orchestratum.MediatR Documentation](src/Orchestratum.MediatR/README.md)

## 🌐 Language Versions

- [English](README.md)
- [Русский](README.ru.md)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🔗 Links

- [GitHub Repository](https://github.com/PogovorovDaniil/Orchestratum)
- [NuGet Package](https://www.nuget.org/packages/Orchestratum/)
