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

    /// <summary>
    /// Primary constructor — uses the booking's PlatformFee/WorkerEarning as
    /// the snapshot, so each ServicePayment locks in the commission that was
    /// in effect at booking creation time. If admin later changes the platform
    /// commission, historical payments stay untouched (only future bookings
    /// pick up the new rate).
    /// </summary>
    public ServicePayment(int bookingId, int clientId, int workerId,
        decimal amount, decimal platformFee, decimal workerEarning,
        string channel,
        string? mercadoPagoPaymentId = null,
        string? mercadoPagoPreferenceId = null)
    {
        if (!PaymentChannel.IsValid(channel))
            throw new ArgumentException($"Invalid payment channel: {channel}");
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");
        if (platformFee < 0 || platformFee > amount)
            throw new ArgumentException("platformFee must be between 0 and amount");
        if (Math.Abs((platformFee + workerEarning) - amount) > 0.02m)
            throw new ArgumentException("platformFee + workerEarning must equal amount");

        BookingId = bookingId;
        ClientId  = clientId;
        WorkerId  = workerId;
        Amount    = amount;
        Channel   = channel;

        // Snapshot from the booking — do NOT recompute.
        PlatformFee   = platformFee;
        WorkerEarning = workerEarning;

        PayoutStatus = ValueObjects.PayoutStatus.Pending;
        PaidAt = DateTimeOffset.UtcNow;
        MercadoPagoPaymentId    = mercadoPagoPaymentId;
        MercadoPagoPreferenceId = mercadoPagoPreferenceId;
    }

    /// <summary>
    /// Legacy constructor — kept for any internal callsite that still passes
    /// a commission rate directly. Prefer the primary constructor that takes
    /// the snapshot fields from the booking.
    /// </summary>
    /// <remarks>Marked obsolete to nudge callers towards the snapshot variant.</remarks>
    [Obsolete("Pass platformFee and workerEarning snapshot from the booking instead.")]
    public ServicePayment(int bookingId, int clientId, int workerId, decimal amount,
        string channel, decimal commissionRate,
        string? mercadoPagoPaymentId = null,
        string? mercadoPagoPreferenceId = null)
        : this(
            bookingId, clientId, workerId,
            amount,
            Math.Round(amount * commissionRate, 2),
            amount - Math.Round(amount * commissionRate, 2),
            channel,
            mercadoPagoPaymentId,
            mercadoPagoPreferenceId)
    { }

    public void MarkPayoutRequested()
    {
        if (PayoutStatus != ValueObjects.PayoutStatus.Pending) return;
        PayoutRequestedAt = DateTimeOffset.UtcNow;
        PayoutCompletedAt = DateTimeOffset.UtcNow;
        PayoutStatus      = ValueObjects.PayoutStatus.Completed;
    }
}
