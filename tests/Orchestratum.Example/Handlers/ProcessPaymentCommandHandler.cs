using Orchestratum.Contract;
using Orchestratum.Example.Commands;
using Orchestratum.Example.Models;
using Orchestratum.Example.Services;

namespace Orchestratum.Example.Handlers;

public class ProcessPaymentCommandHandler : IOrchCommandHandler<ProcessPaymentCommand>
{
    private readonly OrderService _orderService;
    private readonly PaymentService _paymentService;
    private readonly ILogger<ProcessPaymentCommandHandler> _logger;

    public ProcessPaymentCommandHandler(
        OrderService orderService,
        PaymentService paymentService,
        ILogger<ProcessPaymentCommandHandler> logger)
    {
        _orderService = orderService;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<IOrchResult<ProcessPaymentCommand>> Execute(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var (orderId, paymentId) = command.Input;
        _logger.LogInformation("Processing payment {PaymentId} for order {OrderId}", paymentId, orderId);

        var (success, transactionId) = await _paymentService.ProcessPaymentAsync(paymentId);

        if (success)
        {
            _orderService.UpdateOrderStatus(orderId, OrderStatus.PaymentCompleted);
            _logger.LogInformation("Payment successful for order {OrderId}, transaction: {TransactionId}",
                orderId, transactionId);

            return command.CreateResult(
                new ProcessPaymentOutput(true, transactionId),
                OrchResultStatus.Success);
        }

        _orderService.UpdateOrderStatus(orderId, OrderStatus.PaymentFailed);
        _logger.LogWarning("Payment failed for order {OrderId}", orderId);

        return command.CreateResult(
            new ProcessPaymentOutput(false, ""),
            OrchResultStatus.Failed);
    }
}
