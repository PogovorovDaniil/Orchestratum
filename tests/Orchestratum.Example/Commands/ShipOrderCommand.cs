using Orchestratum.Contract;

namespace Orchestratum.Example.Commands;

public record ShipOrderInput(string OrderId);

[OrchCommand("ship_order")]
public class ShipOrderCommand : OrchCommand<ShipOrderInput>
{
    public override TimeSpan Timeout => TimeSpan.FromMinutes(1);
    public override TimeSpan Delay => TimeSpan.FromSeconds(5); // Simulate shipping preparation delay

    protected override IEnumerable<IOrchCommand> OnSuccess()
    {
        yield return new SendNotificationCommand
        {
            Input = new SendNotificationInput(
                Input.OrderId,
                "customer@example.com",
                "Order Shipped",
                $"Your order {Input.OrderId} has been shipped!")
        };
    }
}
