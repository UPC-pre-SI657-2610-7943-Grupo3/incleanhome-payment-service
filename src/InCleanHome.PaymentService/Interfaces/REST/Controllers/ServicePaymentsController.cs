using System.Net.Mime;
using InCleanHome.PaymentService.Domain.Model.Commands;
using InCleanHome.PaymentService.Domain.Model.Queries;
using InCleanHome.PaymentService.Domain.Services;
using InCleanHome.PaymentService.Infrastructure.Pipeline;
using InCleanHome.PaymentService.Interfaces.REST.Resources;
using InCleanHome.PaymentService.Interfaces.REST.Transform;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace InCleanHome.PaymentService.Interfaces.REST.Controllers;

/// <summary>
/// ServicePayment endpoints.
///
///   POST /api/v1/service-payments/booking/{id}/pay-manual - manual channel
///   GET  /api/v1/service-payments/booking/{id}           - is booking paid?
///   GET  /api/v1/service-payments/worker/balance         - worker balance
///   GET  /api/v1/service-payments/worker                 - worker's payments
///   POST /api/v1/service-payments/worker/request-payout  - request payout
///
/// Card payments do NOT go through this controller — they go through
/// MercadoPagoController which calls ConfirmMercadoPagoPaymentCommand.
/// </summary>
[ApiController]
[Route("api/v1/service-payments")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Service Payments — completed-service payments")]
public class ServicePaymentsController(
    IServicePaymentCommandService commandService,
    IServicePaymentQueryService queryService) : ControllerBase
{
    [HttpPost("booking/{bookingId:int}/pay-manual")]
    [SwaggerOperation("Pay Booking (Manual Channel)",
        "Client registers a manual payment: Yape/Plin/Bank. For MercadoPago use /mercadopago/* endpoints.")]
    public async Task<IActionResult> PayManual(int bookingId, [FromBody] PayBookingManualResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsClient()) return StatusCode(403, new { error = "Only clients can pay bookings" });

        try
        {
            var payment = await commandService.Handle(
                new PayBookingCommand(bookingId, current.UserId, body.Channel));
            return Ok(ServicePaymentResourceFromEntityAssembler.ToResourceFromEntity(payment));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpGet("booking/{bookingId:int}")]
    [SwaggerOperation("Get Booking Payment",
        "Returns the payment for a booking, or 404 if unpaid. Used by the frontend to show 'Paid' state.")]
    public async Task<IActionResult> GetByBooking(int bookingId)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        var p = await queryService.Handle(new GetServicePaymentByBookingIdQuery(bookingId));
        if (p is null) return NotFound();

        if (p.ClientId != current.UserId && p.WorkerId != current.UserId && !current.IsAdmin())
            return StatusCode(403, new { error = "Forbidden" });

        return Ok(ServicePaymentResourceFromEntityAssembler.ToResourceFromEntity(p));
    }

    [HttpGet("worker/balance")]
    [SwaggerOperation("Get Worker Balance",
        "Aggregated stats for the logged-in worker: total earnings, platform fee total, pending payout.")]
    public async Task<IActionResult> GetMyBalance()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsWorker()) return StatusCode(403, new { error = "Only workers have a balance" });

        var balance = await queryService.Handle(new GetWorkerBalanceQuery(current.UserId));
        return Ok(WorkerBalanceResourceFromResultAssembler.ToResource(balance));
    }

    [HttpGet("worker")]
    [SwaggerOperation("Get My Service Payments",
        "Lists all payments received by the logged-in worker (desc by date).")]
    public async Task<IActionResult> ListMine()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsWorker()) return StatusCode(403, new { error = "Workers only" });

        var payments = await queryService.Handle(new GetServicePaymentsByWorkerIdQuery(current.UserId));
        return Ok(payments.Select(ServicePaymentResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpPost("worker/request-payout")]
    [SwaggerOperation("Request Payout",
        "Worker requests payout of ALL pending payments. In this simulation payouts are instantaneous.")]
    public async Task<IActionResult> RequestPayout()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsWorker()) return StatusCode(403, new { error = "Workers only" });

        try
        {
            var count = await commandService.Handle(new RequestPayoutCommand(current.UserId));
            return Ok(new { payoutsProcessed = count });
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }
}
