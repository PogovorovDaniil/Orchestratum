using Orchestratum.Contract;
using Orchestratum.Example.Commands;
using Orchestratum.Example.Models;
using Orchestratum.Example.Services;

namespace Orchestratum.Example.Handlers;

public class ShipOrderCommandHandler : IOrchCommandHandler<ShipOrderCommand>
{
    private readonly OrderService _orderService;
    private readonly ILogger<ShipOrderCommandHandler> _logger;

    public ShipOrderCommandHandler(
        OrderService orderService,
        ILogger<ShipOrderCommandHandler> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    public async Task<IOrchResult<ShipOrderCommand>> Execute(
        ShipOrderCommand command,
        CancellationToken cancellationToken)
    {
        var orderId = command.Input.OrderId;
        _logger.LogInformation("Shipping order {OrderId}", orderId);

        await Task.Delay(500, cancellationToken); // Simulate shipping preparation

        _orderService.UpdateOrderStatus(orderId, OrderStatus.Shipped);
        _logger.LogInformation("Order {OrderId} shipped successfully", orderId);

        return command.CreateResult(OrchResultStatus.Success);
    }
}
