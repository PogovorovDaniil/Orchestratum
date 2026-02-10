using Orchestratum.Contract;
using Orchestratum.Tests.Commands;
using Orchestratum.Tests.Misc;

namespace Orchestratum.Tests.Handlers;

public class SendNotificationCommandHandler : IOrchCommandHandler<SendNotificationCommand>
{
    private readonly TestApplication _fixture;

    public SendNotificationCommandHandler(TestApplication fixture)
    {
        _fixture = fixture;
    }

    public Task<IOrchResult<SendNotificationCommand>> Execute(SendNotificationCommand command, CancellationToken cancellationToken)
    {
        _fixture.AddLog($"Notification sent: {command.Input.Message}");
        return Task.FromResult<IOrchResult<SendNotificationCommand>>(command.CreateResult(OrchResultStatus.Success));
    }
}
