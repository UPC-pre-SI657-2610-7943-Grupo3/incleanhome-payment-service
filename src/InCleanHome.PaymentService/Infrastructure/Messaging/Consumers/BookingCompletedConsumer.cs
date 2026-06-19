using InCleanHome.PaymentService.Infrastructure.Messaging.Events;
using MassTransit;

namespace InCleanHome.PaymentService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes <see cref="BookingCompletedEvent"/>. The payment for the booking is
/// initiated by the client (manual channel or MercadoPago), so we don't auto-
/// create a ServicePayment here. We log the event for audit / debugging.
///
/// Future evolution: if we add an "auto-charge on complete" feature, this is
/// where it lives.
/// </summary>
public class BookingCompletedConsumer(
    ILogger<BookingCompletedConsumer> logger) : IConsumer<BookingCompletedEvent>
{
    public Task Consume(ConsumeContext<BookingCompletedEvent> ctx)
    {
        var evt = ctx.Message;
        logger.LogInformation(
            "[BookingCompleted] booking={BookingId} client={ClientId} worker={WorkerId} amount={Amount}",
            evt.BookingId, evt.ClientId, evt.WorkerId, evt.TotalAmount);
        return Task.CompletedTask;
    }
}
