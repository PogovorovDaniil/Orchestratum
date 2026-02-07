# Orchestratum

[![NuGet](https://img.shields.io/nuget/v/Orchestratum.svg)](https://www.nuget.org/packages/Orchestratum/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)

Легковесный оркестратор фоновых задач с персистентностью для .NET приложений со встроенной логикой повторных попыток, таймаутами и сохранением в базе данных через Entity Framework Core.

## 📦 Пакеты

| Пакет | Описание | NuGet |
|-------|----------|-------|
| **Orchestratum** | Основная библиотека оркестрации | [![NuGet](https://img.shields.io/nuget/v/Orchestratum.svg)](https://www.nuget.org/packages/Orchestratum/) |
| **Orchestratum.MediatR** | Интеграция с MediatR | [![NuGet](https://img.shields.io/nuget/v/Orchestratum.MediatR.svg)](https://www.nuget.org/packages/Orchestratum.MediatR/) |

## ✨ Возможности

- **Персистентная очередь задач** - Задачи хранятся в базе данных, переживают перезапуски приложения
- **Автоматические повторы** - Настраиваемая логика повторных попыток для неудавшихся задач
- **Управление таймаутами** - Таймауты выполнения для каждой задачи или по умолчанию
- **Распределенная блокировка** - Предотвращает дублирование выполнения в multi-instance окружении
- **Гибкие исполнители** - Регистрация пользовательских исполнителей для разных типов задач
- **Фоновая обработка** - Работает как hosted service
- **Интеграция с EF Core** - Работает с любой базой данных, поддерживаемой EF Core

## 🚀 Быстрый старт

### Установка

```bash
dotnet add package Orchestratum
# Или с интеграцией MediatR
dotnet add package Orchestratum.MediatR
```

### Базовое использование (без MediatR)

```csharp
// 1. Настройка сервисов
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

    // Настройка параметров
    opts.DefaultTimeout = TimeSpan.FromMinutes(5);
    opts.DefaultRetryCount = 3;
    opts.CommandPollingInterval = TimeSpan.FromSeconds(30);
});

// 2. Инъекция IOrchestrator и добавление задач в очередь
public class MyService
{
    private readonly IOrchestrator _orchestrator;

    public MyService(IOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task SendEmail(string to, string subject, string body)
    {
        // Добавление с настройками по умолчанию
        await _orchestrator.Append("send-email", new EmailData 
        { 
            To = to, 
            Subject = subject, 
            Body = body 
        });

        // Или с пользовательскими настройками таймаута и повторов
        await _orchestrator.Append(
            "send-email", 
            new EmailData { To = to, Subject = subject, Body = body },
            timeout: TimeSpan.FromMinutes(10),
            retryCount: 5
        );
    }
}
```

### Использование с MediatR

```csharp
// 1. Настройка сервисов
services.AddMediatR(opts => 
    opts.RegisterServicesFromAssembly(typeof(Program).Assembly));

services.AddOchestrator((sp, opts) =>
{
    opts.ConfigureDbContext(dbOpts => dbOpts.UseNpgsql("Host=localhost;Database=myapp"));
    opts.RegisterMediatR();  // Включение интеграции с MediatR
});

// 2. Определение MediatR запроса и обработчика
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

// 3. Добавление MediatR запросов в очередь
public class MyService
{
    private readonly IOrchestrator _orchestrator;

    public MyService(IOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public void SendEmail(string to, string subject, string body)
    {
        // Просто добавьте MediatR запрос
        _orchestrator.Append(new SendEmailCommand(to, subject, body));

        // Или с пользовательскими настройками
        _orchestrator.Append(
            new SendEmailCommand(to, subject, body),
            timeout: TimeSpan.FromMinutes(10),
            retryCount: 5
        );
    }
}
```

## 📖 Документация

Подробная документация доступна здесь:
- [Документация Orchestratum Core](src/Orchestratum/README.ru.md)
- [Документация Orchestratum.MediatR](src/Orchestratum.MediatR/README.ru.md)

## 🌐 Языковые версии

- [English](README.md)
- [Русский](README.ru.md)

## 📄 Лицензия

Проект распространяется под лицензией MIT - подробности в файле [LICENSE.txt](LICENSE.txt).

## 🔗 Ссылки

- [GitHub репозиторий](https://github.com/PogovorovDaniil/Orchestratum)
- [NuGet пакет](https://www.nuget.org/packages/Orchestratum/)
