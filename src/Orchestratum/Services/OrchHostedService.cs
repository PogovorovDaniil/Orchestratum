using Microsoft.Extensions.Hosting;

namespace Orchestratum.Services;

internal class OrchHostedService(Orchestratum orchestrator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await orchestrator.RunCommands(stoppingToken);
            orchestrator.ClearCommands();
            await orchestrator.WaitPollingInterval(stoppingToken);
        }
    }
}
