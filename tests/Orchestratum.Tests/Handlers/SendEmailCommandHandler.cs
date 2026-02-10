using Orchestratum.Contract;
using Orchestratum.Tests.Commands;
using Orchestratum.Tests.Misc;

namespace Orchestratum.Tests.Handlers;

public class SendEmailCommandHandler : IOrchCommandHandler<SendEmailCommand>
{
    private readonly TestApplication _fixture;

    public SendEmailCommandHandler(TestApplication fixture)
    {
        _fixture = fixture;
    }

    public Task<IOrchResult<SendEmailCommand>> Execute(SendEmailCommand command, CancellationToken cancellationToken)
    {
        _fixture.AddLog($"Email sent to {command.Input.To}: {command.Input.Subject}");
        return Task.FromResult<IOrchResult<SendEmailCommand>>(command.CreateResult(OrchResultStatus.Success));
    }
}
