using InCleanHome.PaymentService.Domain.Model.Aggregates;
using InCleanHome.PaymentService.Domain.Model.ValueObjects;
using InCleanHome.PaymentService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InCleanHome.PaymentService.Infrastructure.Persistence.Repositories;

public class PaymentMethodRepository(PaymentDbContext context)
    : BaseRepository<PaymentMethod>(context), IPaymentMethodRepository
{
    public async Task<IEnumerable<PaymentMethod>> FindByUserIdAsync(int userId)
        => await Context.Set<PaymentMethod>()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.CreatedDate)
            .ToListAsync();

    public async Task<PaymentMethod?> FindDefaultByUserIdAsync(int userId)
        => await Context.Set<PaymentMethod>()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IsDefault);
}

public class ServicePaymentRepository(PaymentDbContext context)
    : BaseRepository<ServicePayment>(context), IServicePaymentRepository
{
    public async Task<ServicePayment?> FindByBookingIdAsync(int bookingId)
        => await Context.Set<ServicePayment>()
            .FirstOrDefaultAsync(p => p.BookingId == bookingId);

    public async Task<IEnumerable<ServicePayment>> FindByWorkerIdAsync(int workerId)
        => await Context.Set<ServicePayment>()
            .Where(p => p.WorkerId == workerId)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

    public async Task<IEnumerable<ServicePayment>> FindPendingPayoutsByWorkerIdAsync(int workerId)
        => await Context.Set<ServicePayment>()
            .Where(p => p.WorkerId == workerId && p.PayoutStatus == PayoutStatus.Pending)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();
}
