using InCleanHome.PaymentService.Domain.Model.Aggregates;

namespace InCleanHome.PaymentService.Domain.Repositories;

public interface IBaseRepository<TEntity>
{
    Task AddAsync(TEntity entity);
    Task<TEntity?> FindByIdAsync(int id);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<IEnumerable<TEntity>> ListAsync();
}

public interface IUnitOfWork { Task CompleteAsync(); }

public interface IPaymentMethodRepository : IBaseRepository<PaymentMethod>
{
    Task<IEnumerable<PaymentMethod>> FindByUserIdAsync(int userId);
    Task<PaymentMethod?> FindDefaultByUserIdAsync(int userId);
}

public interface IServicePaymentRepository : IBaseRepository<ServicePayment>
{
    Task<ServicePayment?> FindByBookingIdAsync(int bookingId);
    Task<IEnumerable<ServicePayment>> FindByWorkerIdAsync(int workerId);
    Task<IEnumerable<ServicePayment>> FindPendingPayoutsByWorkerIdAsync(int workerId);
}
