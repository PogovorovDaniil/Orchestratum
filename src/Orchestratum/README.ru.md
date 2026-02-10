# Orchestratum

[![NuGet](https://img.shields.io/nuget/v/Orchestratum.svg)](https://www.nuget.org/packages/Orchestratum/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/PogovorovDaniil/Orchestratum)

*[English version](README.md)*

Мощная и гибкая библиотека оркестрации команд для .NET приложений с персистентным хранилищем, автоматическими повторными попытками, поддержкой распределенного выполнения и возможностью создания цепочек команд.

## Возможности

- **Паттерн Command/Handler**: Четкое разделение определения команд и логики выполнения
- **Персистентная очередь команд**: Команды хранятся в базе данных с полным отслеживанием состояния
- **Цепочки команд**: Поддержка условного выполнения команд в зависимости от успеха, неудачи или отмены
- **Автоматические повторы**: Настраиваемая логика повторных попыток с автоматическим управлением
- **Управление таймаутами**: Настройка таймаута для каждой команды с автоматическим определением превышения времени
- **Распределенное выполнение**: Блокировка на уровне базы данных для безопасной работы нескольких экземпляров с маршрутизацией по целевым узлам
- **Типизированные команды**: Типобезопасные определения команд с типами входных и выходных данных
- **Гибкая регистрация**: Автоматическое обнаружение команд или явная регистрация
- **Фоновая обработка**: Работает как hosted service в ASP.NET Core или обычных .NET хостах
- **Интеграция с Entity Framework Core**: Работает с любой базой данных, поддерживаемой EF Core

## Установка

```bash
dotnet add package Orchestratum
```

## Обзор архитектуры

Orchestratum построен на основе паттерна command/handler:

- **Команды** (`IOrchCommand`): Определяют что нужно выполнить, включая входные данные, таймаут, количество повторов и целевой экземпляр
- **Обработчики** (`IOrchCommandHandler<TCommand>`): Реализуют фактическую логику выполнения команд
- **Оркестратор** (`IOrchestratum`): Управляет постановкой команд в очередь и оркестрацией

## Быстрый старт

### 1. Определите команду

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

    // Создать цепочку команд при успехе
    protected override IEnumerable<IOrchCommand> OnSuccess(ReportResult output)
    {
        yield return new SendEmailCommand 
        { 
            Input = new EmailData 
            { 
                To = "admin@example.com",
                Subject = "Отчет сформирован",
                Body = $"Отчет {output.ReportId} был создан"
            }
        };
    }
}
```

### 2. Реализуйте обработчик команды

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

### 3. Настройте сервисы

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices(services =>
{
    // Регистрация сервисов приложения
    services.AddSingleton<IEmailService, EmailService>();
    services.AddSingleton<IReportService, ReportService>();

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
        opts.TablePrefix = "ORCH_"; // Префикс таблиц базы данных
    });
});

builder.Build().Run();
```

### 4. Поставьте команды в очередь

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

## Параметры конфигурации

### Свойства OrchServiceConfiguration

```csharp
services.AddOchestratum(opts =>
{
    // Настройка базы данных (обязательно)
    opts.ConfigureDbContext(db => db.UseNpgsql(connectionString));

    // Интервал опроса новых команд (по умолчанию: 5 секунд)
    opts.CommandPollingInterval = TimeSpan.FromSeconds(5);

    // Буферное время, добавляемое к таймауту команды для истечения блокировки (по умолчанию: 10 секунд)
    opts.LockTimeoutBuffer = TimeSpan.FromSeconds(10);

    // Максимальное количество команд для извлечения за один цикл опроса (по умолчанию: 100)
    opts.MaxCommandPull = 100;

    // Ключ экземпляра для распределенных сценариев (по умолчанию: "default")
    opts.InstanceKey = "worker-1";

    // Префикс таблиц базы данных (по умолчанию: "ORCH_")
    opts.TablePrefix = "ORCHESTRATUM_";
});
```

## Настройка базы данных

### Поддерживаемые базы данных

Любая база данных, поддерживаемая Entity Framework Core:
- PostgreSQL (рекомендуется)
- SQL Server
- MySQL / MariaDB
- Oracle
- И другие...

### Создание миграций

Создайте фабрику времени разработки в вашем проекте:

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

Выполните команды миграции:

```bash
# Добавить миграцию
dotnet ef migrations add InitialOrchestratum --context OrchDbContext

# Применить миграцию
dotnet ef database update --context OrchDbContext

# Удалить последнюю миграцию (если необходимо)
dotnet ef migrations remove --context OrchDbContext
```

### Схема базы данных

Команды хранятся в таблице `{prefix}commands` (по умолчанию: `ORCH_commands`):

| Столбец | Тип | Описание |
|---------|-----|----------|
| `id` | GUID | Уникальный идентификатор команды |
| `target` | string | Ключ целевого экземпляра для маршрутизации |
| `name` | string | Имя команды (из атрибута или по соглашению) |
| `input` | string | JSON-сериализованные входные данные |
| `output` | string | JSON-сериализованные выходные данные |
| `scheduled_at` | DateTimeOffset | Когда команда должна выполниться |
| `timeout` | TimeSpan | Максимальная длительность выполнения |
| `is_running` | bool | Выполняется ли команда в данный момент |
| `running_at` | DateTimeOffset? | Когда началось выполнение |
| `run_expires_at` | DateTimeOffset? | Когда истекает блокировка выполнения |
| `is_completed` | bool | Успешно ли завершена команда |
| `completed_at` | DateTimeOffset? | Когда команда завершилась |
| `is_canceled` | bool | Была ли команда отменена |
| `canceled_at` | DateTimeOffset? | Когда команда была отменена |
| `retries_left` | int | Оставшиеся попытки повтора |
| `is_failed` | bool | Окончательно ли не удалась команда |
| `failed_at` | DateTimeOffset? | Когда команда не удалась |

## Расширенные возможности

### Именование команд

Команды автоматически именуются на основе имени класса:

```csharp
// Автоматическое именование: "send_email"
public class SendEmailCommand : OrchCommand<EmailData> { }

// Явное именование через атрибут
[OrchCommand("email.send")]
public class SendEmailCommand : OrchCommand<EmailData> { }
```

Соглашение об именовании:
1. Удаляет суффикс "Command"
2. Преобразует PascalCase в snake_case
3. Использует нижний регистр

### Цепочки команд

Создавайте цепочки команд на основе результата выполнения:

```csharp
public class ProcessOrderCommand : OrchCommand<OrderData, OrderResult>
{
    // Выполнить эти команды при успехе
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

    // Выполнить эти команды при неудаче
    protected override IEnumerable<IOrchCommand> OnFailure()
    {
        yield return new NotifyAdminCommand
        {
            Input = new AdminNotification { OrderId = Id }
        };
    }

    // Выполнить эти команды при отмене
    protected override IEnumerable<IOrchCommand> OnCancellation()
    {
        yield return new RefundPaymentCommand
        {
            Input = new PaymentRefund { OrderId = Id }
        };
    }
}
```

### Распределенное выполнение

Направляйте команды конкретным экземплярам с помощью свойства `Target`:

```csharp
// Настройка экземпляров
services.AddOchestratum(opts =>
{
    opts.InstanceKey = "email-worker"; // Этот экземпляр обрабатывает команды email
    // ...
});

// Направить команду конкретному экземпляру
var command = new SendEmailCommand
{
    Input = emailData,
    Target = "email-worker" // Будет обработана только экземпляром email-worker
};

await _orchestratum.Push(command);
```

### Поведение повторов

Повторы происходят автоматически:
- Команда завершается неудачей → `RetriesLeft` уменьшается
- Если `RetriesLeft >= 0` → Команда становится доступной для повтора
- Если `RetriesLeft == -1` → Команда помечается как окончательно неудавшаяся
- Команды `OnFailure` ставятся в очередь только после окончательной неудачи

### Обработка таймаутов

Таймауты применяются автоматически:
- Блокировка периодически обновляется во время выполнения
- Если выполнение превышает таймаут → команда отменяется
- Таймаут запускает повтор (если доступны повторы)
- Истечение блокировки позволяет повторно выбрать устаревшие команды

### Статус результата

Команды возвращают статус через `IOrchResult`:

```csharp
public enum OrchResultStatus
{
    Success,    // Команда выполнена успешно
    Cancelled,  // Команда была отменена (таймаут или явно)
    Failed,     // Команда не удалась (исключение или явно)
    NotFound,   // Обработчик не найден
    TimedOut    // Команда превысила таймаут
}
```

### Явная регистрация

Регистрируйте команды и обработчики явно:

```csharp
services.AddOchestratum(opts =>
{
    // Регистрация конкретной команды
    opts.RegisterCommand(typeof(SendEmailCommand));

    // Регистрация конкретного обработчика
    opts.RegisterHandler<SendEmailCommandHandler>();

    // Или регистрация из сборок
    opts.RegisterCommands(Assembly.GetExecutingAssembly());
    opts.RegisterHandlers(Assembly.GetExecutingAssembly());
});
```

## Лучшие практики

1. **Дизайн команд**: Делайте команды маленькими и сосредоточенными на одной ответственности
2. **Идемпотентность**: Проектируйте обработчики идемпотентными, так как команды могут повторяться
3. **Настройка таймаута**: Устанавливайте реалистичные таймауты на основе ожидаемого времени выполнения
4. **Стратегия повторов**: Используйте повторы для временных сбоев, а не для ошибок бизнес-логики
5. **Маршрутизация целей**: Используйте цели для независимого масштабирования конкретных типов команд
6. **Выбор базы данных**: Используйте PostgreSQL или SQL Server для production-сценариев
7. **Мониторинг**: Отслеживайте состояния команд в базе данных для наблюдаемости

## Лицензия

MIT License

## Вклад в проект

Вклады приветствуются! Пожалуйста, не стесняйтесь отправлять Pull Request.
