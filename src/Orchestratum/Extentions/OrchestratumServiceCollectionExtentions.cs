using Microsoft.Extensions.DependencyInjection;
using Orchestratum.Services;

namespace Orchestratum.Extentions;

/// <summary>
/// Extension methods for configuring orchestratum services in IServiceCollection.
/// </summary>
public static class OrchestratumServiceCollectionExtentions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds the orchestrator service to the dependency injection container.
        /// </summary>
        /// <param name="configurationBuilder">Action to configure the orchestrator options.</param>
        public void AddOchestratum(Action<IServiceProvider, OrchestratumConfiguration> configurationBuilder)
        {
            services.AddSingleton(serviceProvider =>
            {
                var configuration = new OrchestratumConfiguration();
                configurationBuilder(serviceProvider, configuration);
                return configuration;
            });
            services.AddSingleton<IOrchestratum, Services.Orchestratum>();
            services.AddHostedService<OrchestratumHostedService>();
        }
    }
}