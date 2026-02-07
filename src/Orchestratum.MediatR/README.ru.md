# Orchestratum.MediatR

[![NuGet](https://img.shields.io/nuget/v/Orchestratum.MediatR.svg)](https://www.nuget.org/packages/Orchestratum.MediatR/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/PogovorovDaniil/Orchestratum)

*[English version](README.md)*

Расширение интеграции с MediatR для Orchestratum - обеспечивает бесшовную интеграцию запросов MediatR с персистентным оркестратором задач.

## Возможности

- **Интеграция с MediatR**: Помещение запросов MediatR в очередь фоновых задач
- **Типобезопасность**: Строго типизированная обработка запросов MediatR
- **Методы расширения**: Простой и интуитивный API с использованием extension methods
- **Все возможности Orchestrator**: Полная поддержка повторов, таймаутов и распределенного выполнения

## Установка

```bash
dotnet add package Orchestratum.MediatR
```

**Примечание**: Этот пакет требует наличия пакетов `Orchestratum` и `MediatR`.

## Быстрый старт

### 1. Регистрация сервисов

```csharp
using Microsoft.Extensions.DependencyInjection;
using Orchestratum.MediatR;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices(services =>
{
    // Регистрация MediatR
    services.AddMediatR(opts => 
        opts.RegisterServicesFromAssembly(typeof(Program).Assembly));
    
    // Регистрация Orchestratum с поддержкой MediatR
    services.AddOchestrator((sp, opts) => opts
        .ConfigureDbContext(opts => opts.UseNpgsql("Host=localhost;Database=myapp"))
        .RegisterMediatR());  // Включение интеграции с MediatR
});

builder.Build().Run();
```

### 2. Определение обработчиков MediatR

```csharp
using MediatR;

// Определение запроса
public record SendEmailCommand(string To, string Subject, string Body) : IRequest;

// Определение обработчика
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
        _logger.LogInformation("Отправка email на {To}", request.To);
        await _emailService.SendAsync(request.To, request.Subject, request.Body, cancellationToken);
    }
}
```

### 3. Постановка запросов MediatR в очередь

```csharp
public class MyService
{
    private readonly IOrchestrator _orchestrator;

    public MyService(IOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public void EnqueueEmails()
    {
        // Постановка запроса MediatR в очередь с настройками по умолчанию
        _orchestrator.Append(new SendEmailCommand(
            "user@example.com", 
            "Привет", 
            "Добро пожаловать в наш сервис!"));
        
        // Постановка в очередь с пользовательским таймаутом и повторами
        _orchestrator.Append(
            new SendEmailCommand("admin@example.com", "Важно", "Критическое уведомление"),
            timeout: TimeSpan.FromMinutes(10),
            retryCount: 5);
    }
}
```

## Методы расширения

Пакет предоставляет удобные методы расширения:

### `Append` для IOrchestrator

Помещает запрос MediatR в очередь фоновых задач:

```csharp
public void Append(
    IRequest request, 
    TimeSpan? timeout = null, 
    int? retryCount = null)
```

**Параметры:**
- `request` - Запрос MediatR для выполнения
- `timeout` - Опциональный таймаут для выполнения запроса (использует значение по умолчанию оркестратора, если не указано)
- `retryCount` - Опциональное количество попыток повтора (использует значение по умолчанию оркестратора, если не указано)

**Пример:**

```csharp
// Простое использование
orchestrator.Append(new MyCommand("data"));

// С пользовательскими настройками
orchestrator.Append(
    new MyCommand("data"), 
    timeout: TimeSpan.FromMinutes(5), 
    retryCount: 3);
```

### `RegisterMediatR` для конфигурации

Регистрирует исполнитель MediatR в конфигурации оркестратора:

```csharp
public void RegisterMediatR()
```

**Пример:**

```csharp
services.AddOchestrator((sp, opts) => opts
    .ConfigureDbContext(opts => opts.UseNpgsql(connectionString))
    .RegisterMediatR());
```

## Расширенные примеры

### Цепочки команд

Создание рабочих процессов, где одна команда запускает другую:

```csharp
public record ProcessOrderCommand(int OrderId) : IRequest;

public class ProcessOrderHandler : IRequestHandler<ProcessOrderCommand>
{
    private readonly IOrchestrator _orchestrator;
    private readonly IOrderService _orderService;

    public ProcessOrderHandler(IOrchestrator orchestrator, IOrderService orderService)
    {
        _orchestrator = orchestrator;
        _orderService = orderService;
    }

    public async Task Handle(ProcessOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderService.ProcessAsync(request.OrderId, cancellationToken);
        
        // Постановка последующих команд в очередь
        _orchestrator.Append(new SendEmailCommand(
            order.CustomerEmail, 
            "Подтверждение заказа", 
            $"Ваш заказ #{order.Id} был обработан"));
        
        _orchestrator.Append(new GenerateInvoiceCommand(order.Id));
    }
}
```

### Запрос с ответом

Для запросов, возвращающих значения, обратите внимание, что ответ не захватывается при постановке в очередь:

```csharp
// Это выполнится, но ответ не будет доступен вызывающему коду
public record GetUserQuery(int UserId) : IRequest<User>;

// При постановке в очередь результат не возвращается
orchestrator.Append(new GetUserQuery(123)); // Результат теряется

// Для запросов с ответами выполняйте их синхронно:
var user = await mediator.Send(new GetUserQuery(123));
```

**Лучшая практика**: Используйте обработчики `IRequest<T>` для синхронных операций, где вам нужен результат немедленно. Используйте `IRequest` (без ответа) для фоновых задач, которые можно поставить в очередь.

## Обработка ошибок

Запросы MediatR получают все преимущества обработки ошибок Orchestratum:

- **Автоматические повторы**: Неудачные запросы автоматически повторяются на основе конфигурации
- **Управление таймаутами**: Запросы, превышающие таймаут, помечаются как неудачные
- **Логирование исключений**: Все исключения логируются через оркестратор

```csharp
// Этот запрос будет повторен 5 раз в случае неудачи
orchestrator.Append(
    new RiskyCommand(), 
    retryCount: 5);
```

## Интеграция с возможностями Orchestratum

Все возможности Orchestratum доступны для запросов MediatR:

- **Распределенная блокировка**: Предотвращает дублирование выполнения на нескольких экземплярах
- **Персистентность в базе данных**: Запросы переживают перезапуск приложения
- **Фоновая обработка**: Выполняется без блокировки основного потока
- **Настраиваемый опрос**: Контроль частоты проверки новых запросов оркестратором

## Требования

- .NET 10.0 или новее
- Пакет Orchestratum
- MediatR 12.5.0 или новее

## Связанные пакеты

- **[Orchestratum](../Orchestratum/README.ru.md)** - Базовый пакет оркестратора

## Лицензия

MIT License

## Вклад в проект

Вклады приветствуются! Пожалуйста, не стесняйтесь отправлять Pull Request.
