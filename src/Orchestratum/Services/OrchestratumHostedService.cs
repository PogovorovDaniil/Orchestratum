using Microsoft.Extensions.Hosting;

namespace Orchestratum.Services;

internal class OrchestratumHostedService(IOrchestratum orchestrator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await orchestrator.SyncCommands(stoppingToken);
            orchestrator.RunCommands(stoppingToken);
            await orchestrator.WaitPollingInterval(stoppingToken);
        }
    }
}
