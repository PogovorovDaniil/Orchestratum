using Orchestratum.Example.Models;
using System.Collections.Concurrent;

namespace Orchestratum.Example.Services;

public class OrderService
{
    private readonly ConcurrentDictionary<string, Order> _orders = new();

    public Order CreateOrder(string customerName, string customerEmail, List<OrderItem> items)
    {
        var order = new Order
        {
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            Items = items,
            TotalAmount = items.Sum(i => i.Price * i.Quantity),
            Status = OrderStatus.Pending
        };

        _orders[order.Id] = order;
        return order;
    }

    public Order? GetOrder(string orderId)
    {
        _orders.TryGetValue(orderId, out var order);
        return order;
    }

    public List<Order> GetAllOrders()
    {
        return _orders.Values.OrderByDescending(o => o.CreatedAt).ToList();
    }

    public void UpdateOrderStatus(string orderId, OrderStatus status)
    {
        if (_orders.TryGetValue(orderId, out var order))
        {
            order.Status = status;
            if (status == OrderStatus.PaymentCompleted ||
                status == OrderStatus.PaymentFailed ||
                status == OrderStatus.Delivered)
            {
                order.ProcessedAt = DateTime.UtcNow;
            }
        }
    }

    public void SetPaymentId(string orderId, string paymentId)
    {
        if (_orders.TryGetValue(orderId, out var order))
        {
            order.PaymentId = paymentId;
        }
    }
}
