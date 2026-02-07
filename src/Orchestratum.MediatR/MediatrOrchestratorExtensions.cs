using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Orchestratum.MediatR;

/// <summary>
/// Extension methods for integrating SimpleOrchestrator with MediatR.
/// </summary>
public static class MediatrOrchestratorExtensions
{
    /// <summary>
    /// The executor key used for MediatR command execution.
    /// </summary>
    public const string MediatrExecutorKey = "mediatr";

    extension(IOrchestrator orchestrator)
    {
        /// <summary>
        /// Appends a MediatR request to the orchestrator queue.
        /// </summary>
        /// <param name="request">The MediatR request to execute.</param>
        /// <param name="timeout">Optional timeout for request execution.</param>
        /// <param name="retryCount">Optional number of retry attempts.</param>
        public void Append(IRequest request, TimeSpan? timeout = null, int? retryCount = null)
        {
            orchestrator.Append(MediatrExecutorKey, request, timeout, retryCount);
        }
    }

    extension(OrchestratorConfiguration orchestratorConfiguration)
    {
        /// <summary>
        /// Registers the MediatR executor in the orchestrator configuration.
        /// This enables the orchestrator to execute MediatR requests.
        /// </summary>
        public void RegisterMediatR()
        {
            orchestratorConfiguration.RegisterExecutor(MediatrExecutorKey, async (sp, data, ct) =>
            {
                var mediator = sp.GetRequiredService<IMediator>();
                await mediator.Send(data, ct);
            });
        }
    }
}
