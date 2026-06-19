namespace InCleanHome.PaymentService.Domain.Model.Queries;

public record GetPaymentMethodsByUserIdQuery(int UserId);
public record GetPaymentMethodByIdQuery(int Id);

public record GetServicePaymentByBookingIdQuery(int BookingId);
public record GetServicePaymentsByWorkerIdQuery(int WorkerId);
public record GetWorkerBalanceQuery(int WorkerId);
