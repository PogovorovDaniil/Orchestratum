using Microsoft.Extensions.DependencyInjection;
using Orchestratum.Services;

namespace Orchestratum.Extentions;

/// <summary>
/// Extension methods for configuring orchestrator services in IServiceCollection.
/// </summary>
public static class OrchestratorServiceCollectionExtentions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds the orchestrator service to the dependency injection container.
        /// </summary>
        /// <param name="configurationBuilder">Action to configure the orchestrator options.</param>
        public void AddOchestrator(Action<IServiceProvider, OrchestratorConfiguration> configurationBuilder)
        {
            services.AddSingleton(serviceProvider =>
            {
                var configuration = new OrchestratorConfiguration();
                configurationBuilder(serviceProvider, configuration);
                return configuration;
            });
            services.AddSingleton<IOrchestrator, Orchestrator>();
            services.AddHostedService<OrchestratorHostedService>();
        }
    }
}