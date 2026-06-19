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

[ApiController]
[Route("api/v1/payment-methods")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Payment methods (off-platform agreements)")]
public class PaymentMethodsController(
    IPaymentMethodCommandService commandService,
    IPaymentMethodQueryService queryService) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation("List My Payment Methods")]
    public async Task<IActionResult> ListMine()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        var pms = await queryService.Handle(new GetPaymentMethodsByUserIdQuery(current.UserId));
        return Ok(pms.Select(PaymentMethodResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("worker/{workerId:int}")]
    [SwaggerOperation("Get Worker Payment Methods",
        "Used by clients when paying — they need to know where to send the money.")]
    public async Task<IActionResult> GetByWorkerId(int workerId)
    {
        var pms = await queryService.Handle(new GetPaymentMethodsByUserIdQuery(workerId));
        return Ok(pms.Select(PaymentMethodResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpPost]
    [SwaggerOperation("Register Payment Method")]
    public async Task<IActionResult> Register([FromBody] RegisterPaymentMethodResource resource)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        try
        {
            var pm = await commandService.Handle(new RegisterPaymentMethodCommand(
                current.UserId, resource.Type, resource.Label, resource.Details, resource.IsDefault));
            return Ok(PaymentMethodResourceFromEntityAssembler.ToResourceFromEntity(pm));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpPatch("{id:int}/default")]
    [SwaggerOperation("Set Default Payment Method")]
    public async Task<IActionResult> SetDefault(int id)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        var pm = await commandService.Handle(new SetDefaultPaymentMethodCommand(current.UserId, id));
        if (pm is null) return NotFound();
        return Ok(PaymentMethodResourceFromEntityAssembler.ToResourceFromEntity(pm));
    }

    [HttpDelete("{id:int}")]
    [SwaggerOperation("Delete Payment Method")]
    public async Task<IActionResult> Delete(int id)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        var ok = await commandService.Handle(new DeletePaymentMethodCommand(current.UserId, id));
        return ok ? NoContent() : NotFound();
    }
}
