using Microsoft.AspNetCore.Mvc;
using Orchestratum.Example.Commands;
using Orchestratum.Example.Models;
using Orchestratum.Example.Services;

namespace Orchestratum.Example.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly IOrchestratum _orchestratum;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        OrderService orderService,
        IOrchestratum orchestratum,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _orchestratum = orchestratum;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var order = _orderService.CreateOrder(
            request.CustomerName,
            request.CustomerEmail,
            request.Items);

        _logger.LogInformation("Created order {OrderId}", order.Id);

        // Schedule order processing
        var command = new ProcessOrderCommand
        {
            Input = new ProcessOrderInput(order.Id)
        };

        await _orchestratum.Push(command);

        _logger.LogInformation("Scheduled processing for order {OrderId}, command {CommandId}",
            order.Id, command.Id);

        return Ok(new
        {
            order.Id,
            commandId = command.Id,
            order.TotalAmount,
            order.Status
        });
    }

    [HttpGet]
    public IActionResult GetOrders()
    {
        var orders = _orderService.GetAllOrders();
        return Ok(orders);
    }

    [HttpGet("{orderId}")]
    public IActionResult GetOrder(string orderId)
    {
        var order = _orderService.GetOrder(orderId);
        if (order == null)
        {
            return NotFound();
        }
        return Ok(order);
    }
}

public class CreateOrderRequest
{
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public List<OrderItem> Items { get; set; } = [];
}
