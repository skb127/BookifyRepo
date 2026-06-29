using System.Collections.Concurrent;
using Bookify.Application.Abstractions.Payments;

namespace Bookify.Application.IntegrationTests.Infrastructure;

public class MockPaymentGateway : IPaymentGateway
{
    public ConcurrentQueue<CreateCheckoutSessionRequest> CheckoutSessionRequests { get; } = new();
    public ConcurrentQueue<string> CancelledPaymentIntents { get; } = new();
    public ConcurrentQueue<string> CapturedPaymentIntents { get; } = new();
    public ConcurrentQueue<(string PaymentIntentId, decimal Amount, string Currency)> RefundRequests { get; } = new();

    public bool CaptureResult { get; set; } = true;
    public bool CancelResult { get; set; } = true;

    public Task<CreateCheckoutSessionResult> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        CheckoutSessionRequests.Enqueue(request);
        return Task.FromResult(new CreateCheckoutSessionResult(
            $"session_{Guid.NewGuid()}",
            "https://checkout.stripe.com/test",
            $"intent_{Guid.NewGuid()}"
        ));
    }

    public Task<bool> CancelPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        CancelledPaymentIntents.Enqueue(paymentIntentId);
        return Task.FromResult(CancelResult);
    }

    public Task<bool> CapturePaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        CapturedPaymentIntents.Enqueue(paymentIntentId);
        return Task.FromResult(CaptureResult);
    }

    public Task<RefundResult> CreateRefundAsync(
        string paymentIntentId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        RefundRequests.Enqueue((paymentIntentId, amount, currency));
        return Task.FromResult(new RefundResult(
            $"ref_{Guid.NewGuid()}",
            "succeeded",
            amount
        ));
    }

    public void Clear()
    {
        CheckoutSessionRequests.Clear();
        CancelledPaymentIntents.Clear();
        CapturedPaymentIntents.Clear();
        RefundRequests.Clear();
        CaptureResult = true;
        CancelResult = true;
    }
}
