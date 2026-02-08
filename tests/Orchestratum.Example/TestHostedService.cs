using Microsoft.Extensions.Hosting;
using Orchestratum.MediatR;

namespace Orchestratum.Example;

public class Test1HostedService(IOrchestratum orchestrator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int iterator = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            iterator++;
            orchestrator.Append(new LogCommand($"iterator - {iterator}"));
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}

public class Test2HostedService(IOrchestratum orchestrator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int iterator = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            iterator++;
            orchestrator.Append(new DelayLogCommand($"iterator2 - {iterator}", TimeSpan.FromSeconds(5)));
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

public class Test3HostedService(IOrchestratum orchestrator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int iterator = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            iterator++;
            orchestrator.Append(new MaybeErrorCommand());
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}

public class Test4HostedService(IOrchestratum orchestrator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        orchestrator.Append(new ChainedCommand(1));
    }
}