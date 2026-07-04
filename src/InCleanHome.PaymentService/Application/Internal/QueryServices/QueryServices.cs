using InCleanHome.PaymentService.Domain.Model.Aggregates;
using InCleanHome.PaymentService.Domain.Model.Queries;
using InCleanHome.PaymentService.Domain.Model.ValueObjects;
using InCleanHome.PaymentService.Domain.Repositories;
using InCleanHome.PaymentService.Domain.Services;

namespace InCleanHome.PaymentService.Application.Internal.QueryServices;

public class PaymentMethodQueryService(IPaymentMethodRepository repository) : IPaymentMethodQueryService
{
    public async Task<IEnumerable<PaymentMethod>> Handle(GetPaymentMethodsByUserIdQuery query)
        => await repository.FindByUserIdAsync(query.UserId);

    public async Task<PaymentMethod?> Handle(GetPaymentMethodByIdQuery query)
        => await repository.FindByIdAsync(query.Id);
}

public class ServicePaymentQueryService(IServicePaymentRepository repository) : IServicePaymentQueryService
{
    public async Task<ServicePayment?> Handle(GetServicePaymentByBookingIdQuery query)
        => await repository.FindByBookingIdAsync(query.BookingId);

    public async Task<IEnumerable<ServicePayment>> Handle(GetServicePaymentsByWorkerIdQuery query)
        => await repository.FindByWorkerIdAsync(query.WorkerId);

    public async Task<WorkerBalanceResult> Handle(GetWorkerBalanceQuery query)
    {
        var payments = (await repository.FindByWorkerIdAsync(query.WorkerId)).ToList();

        // Pending = payments received but not yet paid out to the worker.
        var pending = payments.Where(p => p.PayoutStatus == PayoutStatus.Pending).ToList();
        var pendingPayout = pending.Sum(p => p.WorkerEarning);

        // Stats only count what has ACTUALLY been paid out (post "Solicitar Cobro").
        // Before the worker requests their payout, the dashboard shows 0 — the
        // pending block above is what nudges them to request.
        var paidOut = payments.Where(p => p.PayoutStatus == PayoutStatus.Completed).ToList();
        var totalEarnings    = paidOut.Sum(p => p.WorkerEarning);
        var platformFeeTotal = paidOut.Sum(p => p.PlatformFee);

        return new WorkerBalanceResult(
            TotalEarnings:      totalEarnings,
            PlatformFeeTotal:   platformFeeTotal,
            NetEarnings:        totalEarnings,
            PendingPayout:      pendingPayout,
            PendingPayoutCount: pending.Count,
            CompletedServices:  paidOut.Count);
    }
}
