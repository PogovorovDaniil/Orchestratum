using Orchestratum.Contract;
using Orchestratum.Tests.Commands;
using Orchestratum.Tests.Misc;

namespace Orchestratum.Tests.Handlers;

public class CallExternalApiCommandHandler : IOrchCommandHandler<CallExternalApiCommand>
{
    private readonly TestApplication _fixture;

    public CallExternalApiCommandHandler(TestApplication fixture)
    {
        _fixture = fixture;
    }

    public Task<IOrchResult<CallExternalApiCommand>> Execute(CallExternalApiCommand command, CancellationToken cancellationToken)
    {
        if (command.Input.ShouldFail)
        {
            _fixture.AddLog($"API call failed: {command.Input.Endpoint}");
            return Task.FromResult<IOrchResult<CallExternalApiCommand>>(command.CreateResult(OrchResultStatus.Failed));
        }
        _fixture.AddLog($"API call success: {command.Input.Endpoint}");
        return Task.FromResult<IOrchResult<CallExternalApiCommand>>(command.CreateResult(OrchResultStatus.Success));
    }
}
