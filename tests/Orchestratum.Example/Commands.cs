using MediatR;
using Microsoft.Extensions.Logging;
using Orchestratum.MediatR;

namespace Orchestratum.Example;

public record LogCommand(string Text) : IRequest;
public class LogHandler(ILogger<LogHandler> logger) : IRequestHandler<LogCommand>
{
    public Task Handle(LogCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Log text: '{text}'", request.Text);
        return Task.CompletedTask;
    }
}

public record DelayLogCommand(string Text, TimeSpan TimeSpan) : IRequest;
public class DelayLogHandler(ILogger<LogHandler> logger) : IRequestHandler<DelayLogCommand>
{
    public async Task Handle(DelayLogCommand request, CancellationToken cancellationToken)
    {
        await Task.Delay(request.TimeSpan);
        logger.LogInformation("DelayLog text: '{text}'", request.Text);
    }
}

public record MaybeErrorCommand : IRequest;
public class MaybeErrorHandler : IRequestHandler<MaybeErrorCommand>
{
    public async Task Handle(MaybeErrorCommand request, CancellationToken cancellationToken)
    {
        if (Random.Shared.Next(2) == 1) throw new Exception("MaybeErrorCommand error");
    }
}


public record ChainedCommand(int ChainNumber) : IRequest;
public class ChainedHandler(IOrchestrator orchestrator, ILogger<LogHandler> logger) : IRequestHandler<ChainedCommand>
{
    public async Task Handle(ChainedCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Chain start {number}", request.ChainNumber);
        await Task.Delay(TimeSpan.FromSeconds(10));
        logger.LogInformation("Chain stop {number}", request.ChainNumber);
        orchestrator.Append(new ChainedCommand(request.ChainNumber + 1));
    }
}
