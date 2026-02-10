using Orchestratum.Contract;
using Orchestratum.Example.Commands;
using Orchestratum.Example.Services;

namespace Orchestratum.Example.Handlers;

public class SendNotificationCommandHandler : IOrchCommandHandler<SendNotificationCommand>
{
    private readonly NotificationService _notificationService;
    private readonly ILogger<SendNotificationCommandHandler> _logger;

    public SendNotificationCommandHandler(
        NotificationService notificationService,
        ILogger<SendNotificationCommandHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<IOrchResult<SendNotificationCommand>> Execute(
        SendNotificationCommand command,
        CancellationToken cancellationToken)
    {
        var input = command.Input;
        _logger.LogInformation("Sending notification to {Email}: {Subject}", input.Email, input.Subject);

        await _notificationService.SendEmailAsync(input.Email, input.Subject, input.Message);

        _logger.LogInformation("Notification sent successfully");
        return command.CreateResult(OrchResultStatus.Success);
    }
}
