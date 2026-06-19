using InCleanHome.PaymentService.Domain.Model.Aggregates;
using InCleanHome.PaymentService.Domain.Services;
using InCleanHome.PaymentService.Interfaces.REST.Resources;

namespace InCleanHome.PaymentService.Interfaces.REST.Transform;

public static class PaymentMethodResourceFromEntityAssembler
{
    public static PaymentMethodResource ToResourceFromEntity(PaymentMethod p)
        => new(p.Id, p.Type, p.Label, p.Details, p.IsDefault);
}

public static class ServicePaymentResourceFromEntityAssembler
{
    public static ServicePaymentResource ToResourceFromEntity(ServicePayment p) => new(
        Id:                   p.Id,
        BookingId:            p.BookingId,
        ClientId:             p.ClientId,
        WorkerId:             p.WorkerId,
        Amount:               p.Amount,
        PlatformFee:          p.PlatformFee,
        WorkerEarning:        p.WorkerEarning,
        Channel:              p.Channel,
        PayoutStatus:         p.PayoutStatus,
        PaidAt:               p.PaidAt,
        PayoutCompletedAt:    p.PayoutCompletedAt,
        MercadoPagoPaymentId: p.MercadoPagoPaymentId);
}

public static class WorkerBalanceResourceFromResultAssembler
{
    public static WorkerBalanceResource ToResource(WorkerBalanceResult r) => new(
        TotalEarnings:      r.TotalEarnings,
        PlatformFeeTotal:   r.PlatformFeeTotal,
        NetEarnings:        r.NetEarnings,
        PendingPayout:      r.PendingPayout,
        PendingPayoutCount: r.PendingPayoutCount,
        CompletedServices:  r.CompletedServices);
}
