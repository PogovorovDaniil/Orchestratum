using Orchestratum.Contract;

namespace Orchestratum.Tests.Commands;

public record NotificationData(string Message);

public class SendNotificationCommand : OrchCommand<NotificationData>
{
    public override TimeSpan Delay => TimeSpan.FromSeconds(2);
}
