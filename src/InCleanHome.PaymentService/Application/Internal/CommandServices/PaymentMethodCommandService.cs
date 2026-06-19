using InCleanHome.PaymentService.Domain.Model.Aggregates;
using InCleanHome.PaymentService.Domain.Model.Commands;
using InCleanHome.PaymentService.Domain.Model.ValueObjects;
using InCleanHome.PaymentService.Domain.Repositories;
using InCleanHome.PaymentService.Domain.Services;

namespace InCleanHome.PaymentService.Application.Internal.CommandServices;

public class PaymentMethodCommandService(
    IPaymentMethodRepository repository,
    IUnitOfWork unitOfWork) : IPaymentMethodCommandService
{
    public async Task<PaymentMethod> Handle(RegisterPaymentMethodCommand c)
    {
        if (!PaymentMethodType.IsValid(c.Type))
            throw new Exception("Invalid payment method type");

        var existing = (await repository.FindByUserIdAsync(c.UserId)).ToList();
        var shouldBeDefault = c.IsDefault || existing.Count == 0;

        if (shouldBeDefault)
            foreach (var m in existing.Where(m => m.IsDefault))
            {
                m.UnmarkAsDefault();
                repository.Update(m);
            }

        var pm = new PaymentMethod(c.UserId, c.Type, c.Label, c.Details, shouldBeDefault);
        await repository.AddAsync(pm);
        await unitOfWork.CompleteAsync();
        return pm;
    }

    public async Task<PaymentMethod?> Handle(SetDefaultPaymentMethodCommand c)
    {
        var target = await repository.FindByIdAsync(c.PaymentMethodId);
        if (target is null || target.UserId != c.UserId) return null;

        var others = (await repository.FindByUserIdAsync(c.UserId))
            .Where(m => m.Id != target.Id && m.IsDefault);
        foreach (var m in others)
        {
            m.UnmarkAsDefault();
            repository.Update(m);
        }

        target.MarkAsDefault();
        repository.Update(target);
        await unitOfWork.CompleteAsync();
        return target;
    }

    public async Task<bool> Handle(DeletePaymentMethodCommand c)
    {
        var target = await repository.FindByIdAsync(c.PaymentMethodId);
        if (target is null || target.UserId != c.UserId) return false;

        var wasDefault = target.IsDefault;
        repository.Remove(target);

        if (wasDefault)
        {
            var remaining = (await repository.FindByUserIdAsync(c.UserId))
                .Where(m => m.Id != target.Id).ToList();
            var newDefault = remaining.FirstOrDefault();
            if (newDefault is not null)
            {
                newDefault.MarkAsDefault();
                repository.Update(newDefault);
            }
        }

        await unitOfWork.CompleteAsync();
        return true;
    }
}
