using InCleanHome.PaymentService.Domain.Model.Aggregates;
using InCleanHome.PaymentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using InCleanHome.PaymentService.Domain.Model.Commands;
using InCleanHome.PaymentService.Domain.Model.ValueObjects;
using InCleanHome.PaymentService.Domain.Repositories;
using InCleanHome.PaymentService.Domain.Services;
using InCleanHome.PaymentService.Infrastructure.ExternalServices.BookingService;
using InCleanHome.PaymentService.Infrastructure.ExternalServices.CommunicationService;
using InCleanHome.PaymentService.Infrastructure.Messaging.Events;
using MassTransit;

namespace InCleanHome.PaymentService.Application.Internal.CommandServices;

/// <summary>
/// Command service for ServicePayment.
/// </summary>
/// <remarks>
/// Replaces monolith dependencies:
///   - <c>IBookingRequestRepository</c> → HTTP to Booking Service.
///   - <c>INotificationsContextFacade</c> + <c>IProfilesContextFacade</c> →
///     publish <c>PaymentProcessedEvent</c> (Communication consumes for notify).
///   - <c>ICommissionRateProvider</c> → reads <c>Payment:PlatformFeePercent</c> from Consul.
/// </remarks>
public class ServicePaymentCommandService(
    IServicePaymentRepository repository,
    IBookingServiceClient bookingClient,
    ICommunicationServiceClient communicationClient,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    PaymentDbContext paymentDbContext,
    ILogger<ServicePaymentCommandService> logger) : IServicePaymentCommandService
{
    public async Task<ServicePayment> Handle(PayBookingCommand c)
    {
        if (!PaymentChannel.IsValid(c.Channel))
            throw new InvalidOperationException("Invalid payment channel");

        if (c.Channel == PaymentChannel.MercadoPago)
            throw new InvalidOperationException(
                "Use the Mercado Pago flow for gateway payments (POST /api/v1/mercadopago/...).");

        var bearer = GetBearer();
        var booking = await bookingClient.GetBookingAsync(c.BookingId, bearer)
            ?? throw new InvalidOperationException("Booking not found or not completed");

        if (booking.ClientId != c.ClientId)
            throw new InvalidOperationException("This booking does not belong to you");

        if (!string.Equals(booking.Status, "completed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Booking must be completed before payment");

        var existing = await repository.FindByBookingIdAsync(c.BookingId);
        if (existing is not null)
            throw new InvalidOperationException("This booking has already been paid");

        // Snapshot the commission from the booking — see #6 fix.
        // ServicePayment NEVER recomputes; the rate is locked at booking creation.
        var payment = new ServicePayment(
            booking.Id, booking.ClientId, booking.WorkerId,
            booking.TotalAmount, booking.PlatformFee, booking.WorkerEarning,
            c.Channel);

        await repository.AddAsync(payment);
        await unitOfWork.CompleteAsync();

        await SafePublishAsync(new PaymentProcessedEvent
        {
            PaymentId = payment.Id,
            BookingId = payment.BookingId,
            ClientId  = payment.ClientId,
            WorkerId  = payment.WorkerId,
            Amount    = payment.Amount,
            Channel   = payment.Channel
        });

        // HTTP fallback: tell the worker their service was paid. The broker
        // delivery and this HTTP path both go through; receiver dedups by key.
        await communicationClient.CreateNotificationAsync(
            userId:         payment.WorkerId,
            type:           "service_paid",
            title:          "Servicio pagado",
            body:           $"El cliente pagó tu servicio (S/. {payment.WorkerEarning:0.00} netos). Revisa el detalle en tus solicitudes completadas.",
            link:           "/worker/requests",
            idempotencyKey: $"payment_processed:{payment.Id}");

        return payment;
    }

    public async Task<ServicePayment> Handle(ConfirmMercadoPagoPaymentCommand c)
    {
        var bearer = GetBearer();
        var booking = await bookingClient.GetBookingAsync(c.BookingId, bearer)
            ?? throw new InvalidOperationException("Booking not found or not completed");

        if (booking.ClientId != c.ClientId)
            throw new InvalidOperationException("This booking does not belong to you");

        if (!string.Equals(booking.Status, "completed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Booking must be completed before payment");

        var existing = await repository.FindByBookingIdAsync(c.BookingId);
        if (existing is not null)
            throw new InvalidOperationException("This booking has already been paid");

        // Snapshot the commission from the booking — see #6 fix.
        var payment = new ServicePayment(
            booking.Id, booking.ClientId, booking.WorkerId,
            booking.TotalAmount, booking.PlatformFee, booking.WorkerEarning,
            PaymentChannel.MercadoPago,
            mercadoPagoPaymentId:    c.MercadoPagoPaymentId,
            mercadoPagoPreferenceId: c.MercadoPagoPreferenceId);

        await repository.AddAsync(payment);
        await unitOfWork.CompleteAsync();

        await SafePublishAsync(new PaymentProcessedEvent
        {
            PaymentId = payment.Id,
            BookingId = payment.BookingId,
            ClientId  = payment.ClientId,
            WorkerId  = payment.WorkerId,
            Amount    = payment.Amount,
            Channel   = payment.Channel
        });

        // HTTP fallback.
        await communicationClient.CreateNotificationAsync(
            userId:         payment.WorkerId,
            type:           "service_paid",
            title:          "Servicio pagado",
            body:           $"El cliente pagó tu servicio vía Mercado Pago (S/. {payment.WorkerEarning:0.00} netos).",
            link:           "/worker/requests",
            idempotencyKey: $"payment_processed:{payment.Id}");

        return payment;
    }

    public async Task<int> Handle(RequestPayoutCommand c)
    {
        var pending = (await repository.FindPendingPayoutsByWorkerIdAsync(c.WorkerId)).ToList();
        if (pending.Count == 0) return 0;

        foreach (var p in pending)
        {
            p.MarkPayoutRequested();
            repository.Update(p);
        }
        await unitOfWork.CompleteAsync();

        foreach (var p in pending)
            await SafePublishAsync(new PayoutRequestedEvent
            {
                PaymentId = p.Id,
                WorkerId  = p.WorkerId,
                NetAmount = p.WorkerEarning
            });

        return pending.Count;
    }

    // ──────────────────────────────────────────────────────────────────
    private decimal GetCommissionRate()
    {
        // Read from DB (PlatformSettings table). Falls back to appsettings.json if the table is empty.
        try
        {
            var settings = paymentDbContext.PlatformSettings.AsNoTracking().FirstOrDefault();
            if (settings is not null) return settings.CommissionPercent / 100m;
        }
        catch
        {
            // Table might not exist yet at very first startup; fall through to config fallback.
        }
        var pct = configuration.GetValue<decimal?>("Payment:PlatformFeePercent") ?? 10m;
        return pct / 100m;
    }

    private string GetBearer()
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null) return string.Empty;
        var raw = http.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
        return raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? raw["Bearer ".Length..] : raw;
    }

    private async Task SafePublishAsync<T>(T evt) where T : class
    {
        try { await publishEndpoint.Publish(evt); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish {EventType}", typeof(T).Name);
        }
    }
}
