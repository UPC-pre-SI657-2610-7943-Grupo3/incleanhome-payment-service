namespace InCleanHome.PaymentService.Domain.Services.External;

/// <summary>
/// Domain port for a payment gateway. Only the adapter knows how the provider
/// (MercadoPago / Stripe / etc.) works. The rest of the code depends only on
/// this interface.
/// </summary>
public interface IPaymentGatewayProvider
{
    Task<CreatePaymentIntentResult> CreatePaymentIntentAsync(CreatePaymentIntentRequest request);
    Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentId);
    Task<PaymentStatusResult?> FindApprovedPaymentByExternalReferenceAsync(string externalReference);
    string GetPublicKey();
}

public record CreatePaymentIntentRequest(
    int BookingId,
    decimal Amount,
    string Description,
    string PayerEmail,
    string SuccessUrl,
    string FailureUrl,
    string PendingUrl);

public record CreatePaymentIntentResult(string PreferenceId, string CheckoutUrl);

public record PaymentStatusResult(string PaymentId, string Status, decimal Amount, string ProviderRawStatus);
