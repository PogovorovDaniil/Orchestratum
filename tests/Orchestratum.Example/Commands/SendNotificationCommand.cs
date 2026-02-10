using Orchestratum.Contract;

namespace Orchestratum.Example.Commands;

public record SendNotificationInput(
    string OrderId,
    string Email,
    string Subject,
    string Message);

[OrchCommand("send_notification")]
public class SendNotificationCommand : OrchCommand<SendNotificationInput>
{
    public override TimeSpan Timeout => TimeSpan.FromSeconds(30);
}
