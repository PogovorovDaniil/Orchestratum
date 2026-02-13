# Orchestratum

[![NuGet](https://img.shields.io/nuget/v/Orchestratum.svg)](https://www.nuget.org/packages/Orchestratum/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/PogovorovDaniil/Orchestratum)

Мощная и гибкая библиотека для оркестрации команд в .NET приложениях с персистентным хранилищем, автоматическими повторами, поддержкой распределенного выполнения и возможностью цепочек команд.

## ✨ Возможности

- **Паттерн Command/Handler** - Четкое разделение определения команд и логики их выполнения
- **Персистентная очередь команд** - Команды хранятся в базе данных с полным отслеживанием состояния
- **Цепочки команд** - Поддержка условного выполнения команд на основе успеха, неудачи или отмены
- **Автоматические повторы** - Настраиваемая логика повторных попыток с автоматическим управлением
- **Управление таймаутами** - Настройка таймаутов для каждой команды с автоматическим определением превышения
- **Распределенное выполнение** - Блокировка на уровне базы данных для безопасного развертывания с несколькими инстансами и маршрутизацией по целевым узлам
- **Типизированные команды** - Типобезопасные определения команд с типами входных и выходных данных
- **Гибкая регистрация** - Автоматическое обнаружение команд или явная регистрация
- **Фоновая обработка** - Работает как hosted service в ASP.NET Core или обычных .NET приложениях
- **Интеграция с Entity Framework Core** - Работает с любой базой данных, поддерживаемой EF Core

## 🚀 Быстрый старт

### Установка

```bash
dotnet add package Orchestratum
```

### 1. Определение команды

```csharp
using Orchestratum.Contract;

// Команда только с входными данными
public class SendEmailCommand : OrchCommand<EmailData>
{
    public override TimeSpan Timeout => TimeSpan.FromMinutes(2);
    public override int RetryCount => 5;
}

// Команда с входными и выходными данными
[OrchCommand("generate_report")]
public class GenerateReportCommand : OrchCommand<ReportRequest, ReportResult>
{
    public override TimeSpan Timeout => TimeSpan.FromMinutes(10);

    // Создание цепочки команд при успешном выполнении
    protected override IEnumerable<IOrchCommand> OnSuccess(ReportResult output)
    {
        yield return new SendEmailCommand 
        { 
            Input = new EmailData 
            { 
                To = "admin@example.com",
                Subject = "Отчет сформирован",
                Body = $"Отчет {output.ReportId} был сформирован"
            }
        };
    }
}
```

### 2. Реализация обработчика команды

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

### 3. Настройка сервисов

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices(services =>
{
    // Регистрация сервисов приложения
    services.AddSingleton<IEmailService, EmailService>();

    // Настройка Orchestratum
    services.AddOchestratum(opts =>
    {
        // Настройка базы данных
        opts.ConfigureDbContext(db => 
            db.UseNpgsql("Host=localhost;Database=myapp"));

        // Регистрация команд и обработчиков из сборок
        opts.RegisterCommands(typeof(Program).Assembly);
        opts.RegisterHandlers(typeof(Program).Assembly);

        // Настройка параметров
        opts.CommandPollingInterval = TimeSpan.FromSeconds(5);
        opts.LockTimeoutBuffer = TimeSpan.FromSeconds(10);
        opts.MaxCommandPull = 100;
        opts.InstanceKey = "default"; // Для распределенных сценариев
    });
});

builder.Build().Run();
```

### 4. Добавление команд в очередь

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

## 📖 Документация

Подробная документация доступна здесь:
- [Документация Orchestratum](src/Orchestratum/README.md)

## 🌐 Языковые версии

- [English](README.md)
- [Русский](README.ru.md)

## 📄 Лицензия

Проект распространяется под лицензией MIT - подробности в файле [LICENSE.txt](LICENSE.txt).

## 🔗 Ссылки

- [GitHub репозиторий](https://github.com/PogovorovDaniil/Orchestratum)
- [NuGet пакет](https://www.nuget.org/packages/Orchestratum/)
