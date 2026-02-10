namespace Orchestratum.Example.Services;

public class PaymentService
{
    private readonly Random _random = new();

    public async Task<(bool Success, string PaymentId)> CreatePaymentAsync(string orderId, decimal amount)
    {
        await Task.Delay(500); // Simulate payment gateway call
        var paymentId = $"PAY_{Guid.NewGuid():N}";
        return (true, paymentId);
    }

    public async Task<(bool Success, string TransactionId)> ProcessPaymentAsync(string paymentId)
    {
        await Task.Delay(1000); // Simulate payment processing

        // 90% success rate
        var success = _random.Next(100) < 90;
        var transactionId = success ? $"TXN_{Guid.NewGuid():N}" : "";

        return (success, transactionId);
    }
}
