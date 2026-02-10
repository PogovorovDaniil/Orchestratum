using Orchestratum.Contract;

namespace Orchestratum.Example.Commands;

public record ProcessPaymentInput(string OrderId, string PaymentId);

public record ProcessPaymentOutput(bool Success, string TransactionId);

[OrchCommand("process_payment")]
public class ProcessPaymentCommand : OrchCommand<ProcessPaymentInput, ProcessPaymentOutput>
{
    public override TimeSpan Timeout => TimeSpan.FromMinutes(1);
    public override int RetryCount => 3;

    protected override IEnumerable<IOrchCommand> OnSuccess(ProcessPaymentOutput output)
    {
        yield return new SendNotificationCommand
        {
            Input = new SendNotificationInput(
                Input.OrderId,
                "customer@example.com",
                "Payment Successful",
                $"Your payment for order {Input.OrderId} was successful. Transaction: {output.TransactionId}")
        };

        yield return new ShipOrderCommand
        {
            Input = new ShipOrderInput(Input.OrderId)
        };
    }

    protected override IEnumerable<IOrchCommand> OnFailure()
    {
        yield return new SendNotificationCommand
        {
            Input = new SendNotificationInput(
                Input.OrderId,
                "customer@example.com",
                "Payment Failed",
                $"Payment for order {Input.OrderId} failed. Please try again.")
        };
    }
}
