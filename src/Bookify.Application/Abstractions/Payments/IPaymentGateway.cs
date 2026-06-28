namespace Bookify.Application.Abstractions.Payments;

public interface IPaymentGateway
{
    Task<CreateCheckoutSessionResult> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken cancellationToken = default);

    Task<bool> CancelPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default);

    Task<bool> CapturePaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default);

    Task<RefundResult> CreateRefundAsync(string paymentIntentId, decimal amount, string currency, CancellationToken cancellationToken = default);
}
