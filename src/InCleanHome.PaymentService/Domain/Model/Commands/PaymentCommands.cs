namespace InCleanHome.PaymentService.Domain.Model.Commands;

// PaymentMethod commands
public record RegisterPaymentMethodCommand(int UserId, string Type, string Label, string? Details, bool IsDefault);
public record SetDefaultPaymentMethodCommand(int UserId, int PaymentMethodId);
public record DeletePaymentMethodCommand(int UserId, int PaymentMethodId);

// ServicePayment commands
public record PayBookingCommand(int BookingId, int ClientId, string Channel);

public record ConfirmMercadoPagoPaymentCommand(
    int BookingId,
    int ClientId,
    string MercadoPagoPaymentId,
    string? MercadoPagoPreferenceId);

public record RequestPayoutCommand(int WorkerId);
