using Orchestratum.Contract;
using Orchestratum.Tests.Commands;
using Orchestratum.Tests.Misc;

namespace Orchestratum.Tests.Handlers;

public class ProcessOrderCommandHandler : IOrchCommandHandler<ProcessOrderCommand>
{
    private readonly TestApplication _fixture;

    public ProcessOrderCommandHandler(TestApplication fixture)
    {
        _fixture = fixture;
    }

    public Task<IOrchResult<ProcessOrderCommand>> Execute(ProcessOrderCommand command, CancellationToken cancellationToken)
    {
        _fixture.AddLog($"Processing order: {command.Input.OrderId}");

        if (command.Input.Amount > 0)
        {
            var result = new OrderResult(command.Input.OrderId, true);
            return Task.FromResult<IOrchResult<ProcessOrderCommand>>(command.CreateResult(result, OrchResultStatus.Success));
        }

        var failResult = new OrderResult(command.Input.OrderId, false);
        return Task.FromResult<IOrchResult<ProcessOrderCommand>>(command.CreateResult(failResult, OrchResultStatus.Failed));
    }
}
