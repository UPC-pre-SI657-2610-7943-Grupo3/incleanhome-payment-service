using System.Net.Mime;
using InCleanHome.PaymentService.Domain.Model.Commands;
using InCleanHome.PaymentService.Domain.Repositories;
using InCleanHome.PaymentService.Domain.Services;
using InCleanHome.PaymentService.Domain.Services.External;
using InCleanHome.PaymentService.Infrastructure.ExternalServices.BookingService;
using InCleanHome.PaymentService.Infrastructure.ExternalServices.IamService;
using InCleanHome.PaymentService.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace InCleanHome.PaymentService.Infrastructure.ExternalServices.MercadoPago;

/// <summary>
/// HTTP endpoints for MercadoPago integration.
///
/// Routes:
///   GET  /api/v1/mercadopago/status         — is MP enabled?
///   GET  /api/v1/mercadopago/public-key     — public key for the SDK Bricks
///   POST /api/v1/mercadopago/preference     — create payment preference
///   POST /api/v1/mercadopago/confirm        — confirm a payment (by payment_id)
///   POST /api/v1/mercadopago/confirm-by-booking — verify by external_reference
/// </summary>
[ApiController]
[Route("api/v1/payments/mercadopago")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Mercado Pago Perú — gateway")]
public class MercadoPagoController(
    IPaymentGatewayProvider gateway,
    IOptions<MercadoPagoSettings> settings,
    IBookingServiceClient bookingClient,
    IIamServiceClient iamClient,
    IServicePaymentCommandService paymentCommandService,
    IServicePaymentRepository paymentRepository) : ControllerBase
{
    public record CreatePreferenceRequest(int BookingId);
    public record CreatePreferenceResponse(string PreferenceId, string CheckoutUrl, string PublicKey);

    public record ConfirmRequest(int BookingId, string PaymentId, string? PreferenceId);
    public record ConfirmResponse(int ServicePaymentId, decimal Amount, string Status);

    public record ConfirmByBookingRequest(int BookingId);

    [HttpGet("status")]
    [AllowAnonymous]
    [SwaggerOperation("Mercado Pago Status", "Returns whether MP is enabled (credentials configured).")]
    public IActionResult Status() => Ok(new
    {
        enabled = settings.Value.IsEnabled,
        sandbox = settings.Value.AccessToken.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase),
    });

    [HttpGet("public-key")]
    [SwaggerOperation("Mercado Pago Public Key", "Returns the Public Key for the frontend SDK.")]
    public IActionResult PublicKey()
    {
        var key = gateway.GetPublicKey();
        if (string.IsNullOrEmpty(key))
            return StatusCode(503, new { error = "Mercado Pago no está configurado." });
        return Ok(new { publicKey = key });
    }

    [HttpPost("preference")]
    [SwaggerOperation("Create Preference",
        "Creates a MercadoPago payment preference for a completed booking. " +
        "Returns the checkout URL where the client pays.")]
    public async Task<IActionResult> CreatePreference([FromBody] CreatePreferenceRequest body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsClient()) return Forbid();

        var bearer = GetBearer();
        var booking = await bookingClient.GetBookingAsync(body.BookingId, bearer);
        if (booking is null) return NotFound(new { error = "Booking not found" });
        if (booking.ClientId != current.UserId) return Forbid();
        if (!string.Equals(booking.Status, "completed", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Booking must be completed before payment" });

        var clientEmail = await iamClient.GetUserEmailAsync(current.UserId, bearer)
                       ?? "cliente@incleanhome.com";

        var basePath = settings.Value.FrontendBaseUrl.TrimEnd('/');
        try
        {
            var result = await gateway.CreatePaymentIntentAsync(new CreatePaymentIntentRequest(
                BookingId:   booking.Id,
                Amount:      booking.TotalAmount,
                Description: $"InCleanHome — Servicio #{booking.Id}",
                PayerEmail:  clientEmail,
                SuccessUrl:  $"{basePath}/payment-success?bookingId={booking.Id}",
                FailureUrl:  $"{basePath}/payment-failure?bookingId={booking.Id}",
                PendingUrl:  $"{basePath}/payment-success?bookingId={booking.Id}&pending=1"));

            return Ok(new CreatePreferenceResponse(result.PreferenceId, result.CheckoutUrl, gateway.GetPublicKey()));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("confirm")]
    [SwaggerOperation("Confirm MP Payment",
        "Validates the payment status with MP and persists a ServicePayment if approved.")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmRequest body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsClient()) return Forbid();

        if (string.IsNullOrWhiteSpace(body.PaymentId))
            return BadRequest(new { error = "PaymentId is required" });

        PaymentStatusResult status;
        try { status = await gateway.GetPaymentStatusAsync(body.PaymentId); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }

        if (status.Status != "approved")
            return BadRequest(new { error = $"El pago no está aprobado (estado: {status.ProviderRawStatus})." });

        try
        {
            var payment = await paymentCommandService.Handle(new ConfirmMercadoPagoPaymentCommand(
                BookingId:               body.BookingId,
                ClientId:                current.UserId,
                MercadoPagoPaymentId:    status.PaymentId,
                MercadoPagoPreferenceId: body.PreferenceId));

            return Ok(new ConfirmResponse(payment.Id, payment.Amount, "approved"));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("confirm-by-booking")]
    [SwaggerOperation("Confirm MP Payment by Booking",
        "Looks up an approved payment in MP by external_reference=bookingId. " +
        "Useful in localhost where MP doesn't auto-redirect.")]
    public async Task<IActionResult> ConfirmByBooking([FromBody] ConfirmByBookingRequest body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsClient()) return Forbid();

        var existing = await paymentRepository.FindByBookingIdAsync(body.BookingId);
        if (existing is not null)
            return Ok(new ConfirmResponse(existing.Id, existing.Amount, "approved"));

        var status = await gateway.FindApprovedPaymentByExternalReferenceAsync(body.BookingId.ToString());
        if (status is null)
            return NotFound(new { error = "Todavía no se detecta un pago aprobado para esta reserva en Mercado Pago." });

        try
        {
            var payment = await paymentCommandService.Handle(new ConfirmMercadoPagoPaymentCommand(
                BookingId:               body.BookingId,
                ClientId:                current.UserId,
                MercadoPagoPaymentId:    status.PaymentId,
                MercadoPagoPreferenceId: null));

            return Ok(new ConfirmResponse(payment.Id, payment.Amount, "approved"));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private string GetBearer()
    {
        var raw = Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
        return raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? raw["Bearer ".Length..] : raw;
    }
}
