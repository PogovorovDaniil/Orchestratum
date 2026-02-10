using Orchestratum.Contract;
using Orchestratum.Example.Commands;
using Orchestratum.Example.Models;
using Orchestratum.Example.Services;

namespace Orchestratum.Example.Handlers;

public class ProcessOrderCommandHandler : IOrchCommandHandler<ProcessOrderCommand>
{
    private readonly OrderService _orderService;
    private readonly PaymentService _paymentService;
    private readonly ILogger<ProcessOrderCommandHandler> _logger;

    public ProcessOrderCommandHandler(
        OrderService orderService,
        PaymentService paymentService,
        ILogger<ProcessOrderCommandHandler> logger)
    {
        _orderService = orderService;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<IOrchResult<ProcessOrderCommand>> Execute(
        ProcessOrderCommand command,
        CancellationToken cancellationToken)
    {
        var orderId = command.Input.OrderId;
        _logger.LogInformation("Processing order {OrderId}", orderId);

        var order = _orderService.GetOrder(orderId);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            return command.CreateResult(
                new ProcessOrderOutput(false, null),
                OrchResultStatus.Failed);
        }

        _orderService.UpdateOrderStatus(orderId, OrderStatus.PaymentProcessing);

        var (success, paymentId) = await _paymentService.CreatePaymentAsync(orderId, order.TotalAmount);

        if (success)
        {
            _orderService.SetPaymentId(orderId, paymentId);
            _logger.LogInformation("Order {OrderId} payment created: {PaymentId}", orderId, paymentId);

            return command.CreateResult(
                new ProcessOrderOutput(true, paymentId),
                OrchResultStatus.Success);
        }

        _orderService.UpdateOrderStatus(orderId, OrderStatus.Cancelled);
        return command.CreateResult(
            new ProcessOrderOutput(false, null),
            OrchResultStatus.Failed);
    }
}
