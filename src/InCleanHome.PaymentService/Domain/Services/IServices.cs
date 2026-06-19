using InCleanHome.PaymentService.Domain.Model.Aggregates;
using InCleanHome.PaymentService.Domain.Model.Commands;
using InCleanHome.PaymentService.Domain.Model.Queries;

namespace InCleanHome.PaymentService.Domain.Services;

public interface IPaymentMethodCommandService
{
    Task<PaymentMethod> Handle(RegisterPaymentMethodCommand command);
    Task<PaymentMethod?> Handle(SetDefaultPaymentMethodCommand command);
    Task<bool> Handle(DeletePaymentMethodCommand command);
}

public interface IPaymentMethodQueryService
{
    Task<IEnumerable<PaymentMethod>> Handle(GetPaymentMethodsByUserIdQuery query);
    Task<PaymentMethod?> Handle(GetPaymentMethodByIdQuery query);
}

public interface IServicePaymentCommandService
{
    Task<ServicePayment> Handle(PayBookingCommand command);
    Task<ServicePayment> Handle(ConfirmMercadoPagoPaymentCommand command);
    Task<int> Handle(RequestPayoutCommand command);
}

public interface IServicePaymentQueryService
{
    Task<ServicePayment?> Handle(GetServicePaymentByBookingIdQuery query);
    Task<IEnumerable<ServicePayment>> Handle(GetServicePaymentsByWorkerIdQuery query);
    Task<WorkerBalanceResult> Handle(GetWorkerBalanceQuery query);
}

public record WorkerBalanceResult(
    decimal TotalEarnings,
    decimal PlatformFeeTotal,
    decimal NetEarnings,
    decimal PendingPayout,
    int     PendingPayoutCount,
    int     CompletedServices);
