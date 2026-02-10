using Orchestratum.Contract;

namespace Orchestratum.Example.Commands;

public record ProcessOrderInput(string OrderId);

public record ProcessOrderOutput(bool Success, string? PaymentId);

[OrchCommand("process_order")]
public class ProcessOrderCommand : OrchCommand<ProcessOrderInput, ProcessOrderOutput>
{
    public override TimeSpan Timeout => TimeSpan.FromMinutes(2);
    public override int RetryCount => 2;

    protected override IEnumerable<IOrchCommand> OnSuccess(ProcessOrderOutput output)
    {
        yield return new ProcessPaymentCommand
        {
            Input = new ProcessPaymentInput(Input.OrderId, output.PaymentId!)
        };
    }

    protected override IEnumerable<IOrchCommand> OnFailure()
    {
        yield return new SendNotificationCommand
        {
            Input = new SendNotificationInput(
                Input.OrderId,
                "admin@example.com",
                "Order Processing Failed",
                $"Order {Input.OrderId} failed to process")
        };
    }
}
