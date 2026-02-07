# Orchestratum

[![NuGet](https://img.shields.io/nuget/v/Orchestratum.svg)](https://www.nuget.org/packages/Orchestratum/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/PogovorovDaniil/Orchestratum)

*[English version](README.md)*

Легковесный оркестратор фоновых задач с персистентностью для .NET приложений со встроенной логикой повторных попыток, таймаутами и сохранением в базе данных.

## Возможности

- **Персистентная очередь задач**: Задачи сохраняются в базе данных, обеспечивая надежность при перезапуске приложения
- **Автоматические повторы**: Настраиваемая логика повторных попыток для неудавшихся задач
- **Управление таймаутами**: Установка таймаутов выполнения для отдельных задач или использование значений по умолчанию
- **Распределенная блокировка**: Предотвращает дублирование выполнения задач в многоэкземплярных развертываниях
- **Гибкая система исполнителей**: Регистрация пользовательских исполнителей для различных типов задач
- **Фоновая обработка**: Работает как hosted service в ASP.NET Core или обычных .NET хостах
- **Интеграция с Entity Framework Core**: Работает с любой базой данных, поддерживаемой EF Core

## Установка

```bash
dotnet add package Orchestratum
```

## Быстрый старт

### 1. Настройка сервисов

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
            // Ваша логика задачи здесь
            var myData = (MyTaskData)data;
            await ProcessTask(myData);
        }));
});

builder.Build().Run();
```

### 2. Постановка задач в очередь

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
        // Поставить задачу в очередь с настройками по умолчанию
        await _orchestrator.Append("my-task", new MyTaskData { Value = "Hello" });

        // Поставить в очередь с пользовательским таймаутом и количеством повторов
        await _orchestrator.Append(
            "my-task", 
            new MyTaskData { Value = "World" },
            timeout: TimeSpan.FromMinutes(5),
            retryCount: 5
        );
    }
}
```

## Параметры конфигурации

Оркестратор предоставляет несколько параметров конфигурации:

```csharp
services.AddOchestrator((sp, opts) => opts
    .ConfigureDbContext(opts => opts.UseNpgsql(connectionString))
    .RegisterExecutor("executor-key", executorDelegate)
    .With(o =>
    {
        // Интервал опроса новых команд (по умолчанию: 1 минута)
        o.CommandPollingInterval = TimeSpan.FromSeconds(30);
        
        // Буферное время для таймаута блокировки (по умолчанию: 1 секунда)
        o.LockTimeoutBuffer = TimeSpan.FromSeconds(2);
        
        // Таймаут по умолчанию для задач (по умолчанию: 1 минута)
        o.DefaultTimeout = TimeSpan.FromMinutes(5);
        
        // Количество повторов по умолчанию (по умолчанию: 3)
        o.DefaultRetryCount = 5;
    }));
```

## Настройка базы данных

Orchestratum использует Entity Framework Core для сохранения данных. Вам необходимо создать требуемые таблицы в вашей базе данных.

### Поддерживаемые базы данных

Может использоваться любая база данных, поддерживаемая Entity Framework Core:
- PostgreSQL (рекомендуется)
- SQL Server
- MySQL
- SQLite
- И другие...

### Создание миграций

Поскольку `OrchestratorDbContext` находится в библиотеке, вам нужно создать фабрику времени разработки в вашем основном проекте для включения миграций:

**Шаг 1:** Создайте класс фабрики в вашем проекте:

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
        
        // Настройте ваш провайдер базы данных
        optionsBuilder.UseNpgsql("Host=localhost;Database=myapp;Username=user;Password=pass", 
            opts => opts.MigrationsAssembly(typeof(OrchestratorDbContextFactory).Assembly.GetName().Name));

        return new OrchestratorDbContext(optionsBuilder.Options);
    }
}
```

**Шаг 2:** Выполните команды миграции:

```bash
# Добавить миграцию
dotnet ef migrations add InitialOrchestrator --context OrchestratorDbContext

# Применить миграцию
dotnet ef database update --context OrchestratorDbContext

# Удалить последнюю миграцию (если необходимо)
dotnet ef migrations remove --context OrchestratorDbContext
```

### Схема базы данных

Оркестратор хранит команды в таблице `orchestrator_commands` со следующими столбцами:
- `id` - Уникальный идентификатор команды (GUID)
- `executor` - Ключ исполнителя
- `data_type` - Сериализованный тип данных
- `data` - JSON сериализованные данные команды
- `timeout` - Таймаут выполнения
- `retries_left` - Оставшиеся попытки повтора
- `is_running` - Флаг статуса выполнения
- `is_completed` - Флаг статуса завершения
- `is_failed` - Флаг статуса неудачи
- `locked_until` - Временная метка истечения блокировки

## Расширенное использование

### Пользовательские исполнители

Вы можете зарегистрировать несколько исполнителей для разных типов задач:

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

### Обработка ошибок

Неудачные задачи автоматически повторяются на основе настроенного количества повторов. После исчерпания всех повторов задача помечается как неудачная и больше не будет повторяться.

```csharp
// Эта задача будет повторена 5 раз в случае неудачи
await _orchestrator.Append("my-task", data, retryCount: 5);
```

### Обработка таймаутов

Каждая задача может иметь свой собственный таймаут. Если задача превышает таймаут, она будет помечена как неудачная и повторена (если доступны повторы).

```csharp
// Эта задача превысит время ожидания через 10 минут
await _orchestrator.Append("long-task", data, timeout: TimeSpan.FromMinutes(10));
```

### Распределенные сценарии

Orchestratum использует блокировку на уровне базы данных, чтобы предотвратить многократное выполнение одной и той же задачи в распределенных сценариях. Это особенно полезно при запуске нескольких экземпляров вашего приложения.

## Расширения

- **[Orchestratum.MediatR](../Orchestratum.MediatR/README.ru.md)** - Интеграция с MediatR для паттернов команд/запросов

## Лицензия

MIT License

## Вклад в проект

Вклады приветствуются! Пожалуйста, не стесняйтесь отправлять Pull Request.
