using System.Net.Mime;
using InCleanHome.PaymentService.Infrastructure.Persistence;
using InCleanHome.PaymentService.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace InCleanHome.PaymentService.Interfaces.REST.Controllers;

/// <summary>
///   Public-read endpoint that exposes the platform commission rate.
///   Used by Booking Service (and others) to snapshot the current rate at
///   booking creation time, so commission changes only affect new bookings.
/// </summary>
/// <remarks>
///   Read-only and non-admin. The write side (update commission) stays on
///   <see cref="AdminSettingsController"/> which still requires admin role.
/// </remarks>
[ApiController]
[Route("api/v1/payments/commission-rate")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Payments — public commission rate")]
public class CommissionRateController(
    PaymentDbContext db,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation("Get current commission rate",
        "Returns the platform commission percent + decimal rate. " +
        "Booking Service uses this when creating a booking to snapshot the rate. " +
        "Requires authentication but NOT admin role.")]
    public async Task<IActionResult> Get()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        var settings = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync();
        var percent  = settings?.CommissionPercent
                       ?? configuration.GetValue<decimal?>("Payment:PlatformFeePercent")
                       ?? 10m;

        return Ok(new
        {
            commissionPercent = percent,
            commissionRate    = percent / 100m
        });
    }
}
