namespace InCleanHome.PaymentService.Interfaces.REST.Resources;

public record RegisterPaymentMethodResource(string Type, string Label, string? Details, bool IsDefault);
public record PaymentMethodResource(int Id, string Type, string Label, string Details, bool IsDefault);

public record PayBookingManualResource(string Channel);

public record ServicePaymentResource(
    int Id,
    int BookingId,
    int ClientId,
    int WorkerId,
    decimal Amount,
    decimal PlatformFee,
    decimal WorkerEarning,
    string Channel,
    string PayoutStatus,
    DateTimeOffset PaidAt,
    DateTimeOffset? PayoutCompletedAt,
    string? MercadoPagoPaymentId);

public record WorkerBalanceResource(
    decimal TotalEarnings,
    decimal PlatformFeeTotal,
    decimal NetEarnings,
    decimal PendingPayout,
    int     PendingPayoutCount,
    int     CompletedServices);
