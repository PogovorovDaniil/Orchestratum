# Orchestratum.MediatR

[![NuGet](https://img.shields.io/nuget/v/Orchestratum.MediatR.svg)](https://www.nuget.org/packages/Orchestratum.MediatR/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/PogovorovDaniil/Orchestratum)

*[Русская версия](README.ru.md)*

MediatR integration extension for Orchestratum - enables seamless integration of MediatR requests with the persistent task orchestrator.

## Features

- **MediatR Integration**: Queue MediatR requests as background tasks
- **Type-Safe**: Strongly-typed MediatR request handling
- **Extension Methods**: Simple and intuitive API using extension methods
- **All Orchestratum Features**: Full support for retries, timeouts, and distributed execution

## Installation

```bash
dotnet add package Orchestratum.MediatR
```

**Note**: This package requires both `Orchestratum` and `MediatR` packages.

## Quick Start

### 1. Register Services

```csharp
using Microsoft.Extensions.DependencyInjection;
using Orchestratum.MediatR;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices(services =>
{
    // Register MediatR
    services.AddMediatR(opts => 
        opts.RegisterServicesFromAssembly(typeof(Program).Assembly));
    
    // Register Orchestratum with MediatR support
    services.AddOrchestratum((sp, opts) => opts
        .ConfigureDbContext(opts => opts.UseNpgsql("Host=localhost;Database=myapp"))
        .RegisterMediatR());  // Enable MediatR integration
});

builder.Build().Run();
```

### 2. Define MediatR Handlers

```csharp
using MediatR;

// Define a request
public record SendEmailCommand(string To, string Subject, string Body) : IRequest;

// Define a handler
public class SendEmailHandler : IRequestHandler<SendEmailCommand>
{
    private readonly ILogger<SendEmailHandler> _logger;
    private readonly IEmailService _emailService;

    public SendEmailHandler(ILogger<SendEmailHandler> logger, IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }

    public async Task Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending email to {To}", request.To);
        await _emailService.SendAsync(request.To, request.Subject, request.Body, cancellationToken);
    }
}
```

### 3. Queue MediatR Requests

```csharp
public class MyService
{
    private readonly IOrchestratum _orchestratum;

    public MyService(IOrchestratum orchestratum)
    {
        _orchestratum = orchestratum;
    }

    public void EnqueueEmails()
    {
        // Queue a MediatR request with default settings
        _orchestratum.Append(new SendEmailCommand(
            "user@example.com", 
            "Hello", 
            "Welcome to our service!"));

        // Queue with custom timeout and retry settings
        _orchestratum.Append(
            new SendEmailCommand("admin@example.com", "Important", "Critical notification"),
            timeout: TimeSpan.FromMinutes(10),
            retryCount: 5);
    }
}
```

## Extension Methods

The package provides convenient extension methods:

### `Append` for IOrchestratum

Queue a MediatR request as a background task:

```csharp
public void Append(
    IRequest request, 
    TimeSpan? timeout = null, 
    int? retryCount = null)
```

**Parameters:**
- `request` - The MediatR request to execute
- `timeout` - Optional timeout for request execution (uses orchestrator default if not specified)
- `retryCount` - Optional number of retry attempts (uses orchestrator default if not specified)

**Example:**

```csharp
// Simple usage
orchestrator.Append(new MyCommand("data"));

// With custom settings
orchestrator.Append(
    new MyCommand("data"), 
    timeout: TimeSpan.FromMinutes(5), 
    retryCount: 3);
```

### `RegisterMediatR` for Configuration

Register the MediatR executor in orchestrator configuration:

```csharp
public void RegisterMediatR()
```

**Example:**

```csharp
services.AddOrchestratum((sp, opts) => opts
    .ConfigureDbContext(opts => opts.UseNpgsql(connectionString))
    .RegisterMediatR());
```

## Advanced Examples

### Chained Commands

Create workflows where one command triggers another:

```csharp
public record ProcessOrderCommand(int OrderId) : IRequest;

public class ProcessOrderHandler : IRequestHandler<ProcessOrderCommand>
{
    private readonly IOrchestratum _orchestratum;
    private readonly IOrderService _orderService;

    public ProcessOrderHandler(IOrchestratum orchestratum, IOrderService orderService)
    {
        _orchestratum = orchestratum;
        _orderService = orderService;
    }

    public async Task Handle(ProcessOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderService.ProcessAsync(request.OrderId, cancellationToken);

        // Queue follow-up commands
        _orchestratum.Append(new SendEmailCommand(
            order.CustomerEmail, 
            "Order Confirmation", 
            $"Your order #{order.Id} has been processed"));

        _orchestratum.Append(new GenerateInvoiceCommand(order.Id));
    }
}
```

### Request with Response

For requests that return values, note that the response is not captured when queued:

```csharp
// This will execute, but the response won't be available to the caller
public record GetUserQuery(int UserId) : IRequest<User>;

// When queued, the result is not returned
orchestrator.Append(new GetUserQuery(123)); // Result is lost

// For requests with responses, execute them synchronously instead:
var user = await mediator.Send(new GetUserQuery(123));
```

**Best Practice**: Use `IRequest<T>` handlers for synchronous operations where you need the result immediately. Use `IRequest` (without response) for background tasks that can be queued.

## Error Handling

MediatR requests benefit from all Orchestratum error handling features:

- **Automatic Retries**: Failed requests are automatically retried based on configuration
- **Timeout Management**: Requests that exceed timeout are marked as failed
- **Exception Logging**: All exceptions are logged through the orchestrator

```csharp
// This request will be retried 5 times if it fails
orchestrator.Append(
    new RiskyCommand(), 
    retryCount: 5);
```

## Integration with Orchestratum Features

All Orchestratum features are available for MediatR requests:

- **Distributed Locking**: Prevents duplicate execution across multiple instances
- **Database Persistence**: Requests survive application restarts
- **Background Processing**: Executes without blocking the main thread
- **Configurable Polling**: Control how often the orchestrator checks for new requests

## Requirements

- .NET 10.0 or later
- Orchestratum package
- MediatR 12.5.0 or later

## Related Packages

- **[Orchestratum](../Orchestratum/README.md)** - Base orchestrator package

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
