using System.Net.Mime;
using InCleanHome.PaymentService.Domain.Model.Aggregates;
using InCleanHome.PaymentService.Infrastructure.Persistence;
using InCleanHome.PaymentService.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace InCleanHome.PaymentService.Interfaces.REST.Controllers;

/// <summary>
/// Admin-only configuration of platform-wide payment settings (e.g. commission percent).
/// Persisted in the Payment Service database (PlatformSettings table) so changes are
/// immediate, transactional, and survive restarts — without touching Consul KV.
/// </summary>
[ApiController]
[Route("api/v1/admin/settings")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Admin — platform-wide payment settings")]
public class AdminSettingsController(
    PaymentDbContext db,
    IConfiguration configuration,
    ILogger<AdminSettingsController> logger) : ControllerBase
{
    /// <summary>
    /// Returns current commission percent (and its allowed bounds).
    /// </summary>
    [HttpGet]
    [SwaggerOperation("Get Platform Settings",
        "Returns the current commission percent and its allowed min/max. " +
        "If the table is empty, it is seeded with the value from appsettings.")]
    public async Task<IActionResult> Get()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        var settings = await GetOrSeedAsync();
        return Ok(BuildResponse(settings));
    }

    /// <summary>
    /// Updates the commission percent.
    /// </summary>
    /// <remarks>Body: { "commissionPercent": 12 }</remarks>
    [HttpPut]
    [SwaggerOperation("Update Platform Settings",
        "Updates the commission percent. Value must be between MinPercent and MaxPercent.")]
    public async Task<IActionResult> Update([FromBody] UpdatePlatformSettingsRequest body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        var (min, max) = GetBounds();
        if (body.CommissionPercent < min || body.CommissionPercent > max)
            return BadRequest(new { error = $"Commission must be between {min} and {max}" });

        var settings = await GetOrSeedAsync();
        try
        {
            settings.UpdateCommission(body.CommissionPercent);
            await db.SaveChangesAsync();
            logger.LogInformation("Platform commission updated to {Percent}% by admin {AdminId}",
                body.CommissionPercent, current.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update platform commission");
            return BadRequest(new { error = ex.Message });
        }

        return Ok(BuildResponse(settings));
    }

    // ────────────────────────────────────────────────────────────────────

    private async Task<PlatformSettings> GetOrSeedAsync()
    {
        var existing = await db.PlatformSettings.FirstOrDefaultAsync();
        if (existing is not null) return existing;

        var defaultPct = configuration.GetValue<decimal?>("Payment:PlatformFeePercent") ?? 10m;
        var fresh = new PlatformSettings(defaultPct);
        db.PlatformSettings.Add(fresh);
        await db.SaveChangesAsync();
        return fresh;
    }

    private (decimal Min, decimal Max) GetBounds() =>
        (configuration.GetValue<decimal?>("Payment:MinCommissionPercent") ?? 0m,
         configuration.GetValue<decimal?>("Payment:MaxCommissionPercent") ?? 30m);

    private object BuildResponse(PlatformSettings s)
    {
        var (min, max) = GetBounds();
        return new
        {
            commissionPercent = s.CommissionPercent,
            commissionRate    = s.CommissionPercent / 100m,
            minPercent        = min,
            maxPercent        = max,
            updatedAt         = s.UpdatedDate ?? s.CreatedDate
        };
    }
}

public record UpdatePlatformSettingsRequest(decimal CommissionPercent);
