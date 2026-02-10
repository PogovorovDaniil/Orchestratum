using Orchestratum.Contract;

namespace Orchestratum.Tests.Commands;

public record OrderData(string OrderId, decimal Amount);
public record OrderResult(string OrderId, bool Success);

public class ProcessOrderCommand : OrchCommand<OrderData, OrderResult>
{
    protected override IEnumerable<IOrchCommand> OnSuccess(OrderResult output)
    {
        yield return new SendEmailCommand
        {
            Input = new EmailData($"customer@example.com", "Order Confirmed", $"Order {output.OrderId} confirmed")
        };
    }

    protected override IEnumerable<IOrchCommand> OnFailure()
    {
        yield return new SendEmailCommand
        {
            Input = new EmailData("admin@example.com", "Order Failed", $"Order {Input.OrderId} failed")
        };
    }
}
