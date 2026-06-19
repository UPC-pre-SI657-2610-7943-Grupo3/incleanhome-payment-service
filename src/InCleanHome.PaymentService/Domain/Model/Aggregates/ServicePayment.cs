using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.CreatedUpdatedDate.Contracts;
using InCleanHome.PaymentService.Domain.Model.ValueObjects;

namespace InCleanHome.PaymentService.Domain.Model.Aggregates;

/// <summary>
///     Registers the payment of a completed booking. One per booking.
/// </summary>
public class ServicePayment : IEntityWithCreatedUpdatedDate
{
    public int Id { get; private set; }
    public int BookingId { get; private set; }
    public int ClientId  { get; private set; }
    public int WorkerId  { get; private set; }

    public decimal Amount        { get; private set; }
    public decimal PlatformFee   { get; private set; }
    public decimal WorkerEarning { get; private set; }

    public string Channel      { get; private set; } = PaymentChannel.Yape;
    public string PayoutStatus { get; private set; } = ValueObjects.PayoutStatus.NotApplicable;

    public DateTimeOffset PaidAt { get; private set; }
    public DateTimeOffset? PayoutRequestedAt { get; private set; }
    public DateTimeOffset? PayoutCompletedAt { get; private set; }

    public string? MercadoPagoPaymentId { get; private set; }
    public string? MercadoPagoPreferenceId { get; private set; }

    [Column("CreatedAt")] public DateTimeOffset? CreatedDate { get; set; }
    [Column("UpdatedAt")] public DateTimeOffset? UpdatedDate { get; set; }

    public ServicePayment() { }

    public ServicePayment(int bookingId, int clientId, int workerId, decimal amount,
        string channel, decimal commissionRate,
        string? mercadoPagoPaymentId = null,
        string? mercadoPagoPreferenceId = null)
    {
        if (!PaymentChannel.IsValid(channel))
            throw new ArgumentException($"Invalid payment channel: {channel}");
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");
        if (commissionRate < 0m || commissionRate > 1m)
            throw new ArgumentException("commissionRate must be between 0 and 1");

        BookingId = bookingId;
        ClientId  = clientId;
        WorkerId  = workerId;
        Amount    = amount;
        Channel   = channel;

        PlatformFee   = Math.Round(amount * commissionRate, 2);
        WorkerEarning = amount - PlatformFee;

        PayoutStatus = ValueObjects.PayoutStatus.Pending;
        PaidAt = DateTimeOffset.UtcNow;
        MercadoPagoPaymentId    = mercadoPagoPaymentId;
        MercadoPagoPreferenceId = mercadoPagoPreferenceId;
    }

    public void MarkPayoutRequested()
    {
        if (PayoutStatus != ValueObjects.PayoutStatus.Pending) return;
        PayoutRequestedAt = DateTimeOffset.UtcNow;
        PayoutCompletedAt = DateTimeOffset.UtcNow;
        PayoutStatus      = ValueObjects.PayoutStatus.Completed;
    }
}
