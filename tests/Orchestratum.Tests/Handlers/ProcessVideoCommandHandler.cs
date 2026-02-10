using Orchestratum.Contract;
using Orchestratum.Tests.Commands;
using Orchestratum.Tests.Misc;

namespace Orchestratum.Tests.Handlers;

public class ProcessVideoCommandHandler : IOrchCommandHandler<ProcessVideoCommand>
{
    private readonly TestApplication _fixture;

    public ProcessVideoCommandHandler(TestApplication fixture)
    {
        _fixture = fixture;
    }

    public async Task<IOrchResult<ProcessVideoCommand>> Execute(ProcessVideoCommand command, CancellationToken cancellationToken)
    {
        _fixture.AddLog($"Processing video: {command.Input.VideoId}");
        await Task.Delay(command.Input.ProcessingTimeMs, cancellationToken);
        _fixture.AddLog($"Video {command.Input.VideoId} processed");
        return command.CreateResult(OrchResultStatus.Success);
    }
}
