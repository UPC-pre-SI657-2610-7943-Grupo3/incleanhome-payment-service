namespace InCleanHome.PaymentService.Infrastructure.Messaging.Events;

// Published by Payment Service 

public record PaymentProcessedEvent
{
    public int PaymentId { get; init; }
    public int BookingId { get; init; }
    public int ClientId { get; init; }
    public int WorkerId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string WorkerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Channel { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public record PaymentFailedEvent
{
    public int BookingId { get; init; }
    public int ClientId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public record PayoutRequestedEvent
{
    public int PaymentId { get; init; }
    public int WorkerId { get; init; }
    public decimal NetAmount { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

// Consumed by Payment Service (duplicated from publisher) 

/// <summary>
/// Published by Booking Service when a worker marks a booking complete.
/// Used by Payment to know which bookings are payable. The current behaviour
/// does NOT auto-create a ServicePayment on this event — payments are still
/// initiated by the client (manual channel or MercadoPago). This consumer
/// only logs/audits the event.
/// </summary>
public record BookingCompletedEvent
{
    public int BookingId { get; init; }
    public int ClientId { get; init; }
    public int WorkerId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string WorkerName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public decimal PlatformFee { get; init; }
    public decimal WorkerEarning { get; init; }
    public int PaymentMethodId { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
