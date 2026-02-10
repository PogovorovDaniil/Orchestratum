using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchestratum;
using Orchestratum.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Delegate for configuring Orchestratum service options.
/// </summary>
/// <param name="cfg">The configuration object to configure.</param>
public delegate void OrchServiceConfigurationBuilder(OrchServiceConfiguration cfg);

/// <summary>
/// Extension methods for configuring Orchestratum services.
/// </summary>
public static class OrchServiceCollectionExtentions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Orchestratum services to the specified service collection.
        /// </summary>
        /// <param name="configurationBuilder">The configuration builder to set up orchestration options.</param>
        public void AddOchestratum(OrchServiceConfigurationBuilder configurationBuilder)
        {
            var configuration = new OrchServiceConfiguration();
            configurationBuilder(configuration);

            services.AddSingleton(configuration);
            foreach (var descriptor in configuration.HandlerDescriptors) services.TryAdd(descriptor);
            foreach (var descriptor in configuration.CommandDescriptors) services.TryAdd(descriptor);
            services.AddSingleton(configuration.ContextOptionsBuilder.Options);
            services.AddSingleton<OrchDataService>();
            services.AddTransient<CommandExecutor>();
            services.AddSingleton<Orchestratum.Services.Orchestratum>();
            services.AddSingleton<IOrchestratum>(sp => sp.GetRequiredService<Orchestratum.Services.Orchestratum>());
            services.AddHostedService<OrchHostedService>();
        }
    }
}
