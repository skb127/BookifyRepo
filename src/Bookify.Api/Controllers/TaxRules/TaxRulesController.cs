using Asp.Versioning;
using Bookify.Application.TaxRules;
using Bookify.Application.TaxRules.CreateTaxRule;
using Bookify.Application.TaxRules.DeactivateTaxRule;
using Bookify.Application.TaxRules.GetTaxRule;
using Bookify.Application.TaxRules.GetTaxRules;
using Bookify.Application.TaxRules.UpdateTaxRule;
using Bookify.Domain.Abstractions;
using Bookify.Domain.TaxRules;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bookify.Api.Controllers.TaxRules;

[Authorize]
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route("api/v{version:apiVersion}/tax-rules")]
public sealed class TaxRulesController : ControllerBase
{
    private readonly ISender _sender;

    public TaxRulesController(ISender sender) => _sender = sender;

    [HttpGet]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> GetTaxRules([FromQuery] string? countryCode, CancellationToken cancellationToken)
    {
        var query = new GetTaxRulesQuery(countryCode);
        Result<IReadOnlyList<TaxRuleResponse>> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> GetTaxRule(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetTaxRuleQuery(id);
        Result<TaxRuleResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == TaxRuleErrors.NotFound)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    detail: result.Error.Name,
                    title: result.Error.Code);
            }

            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> CreateTaxRule(CreateTaxRuleRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateTaxRuleCommand(
            request.CountryCode,
            request.Region,
            request.City,
            request.RateValue,
            request.RateType,
            request.Name,
            request.EffectiveFrom,
            request.EffectiveTo);

        Result<Guid> result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return CreatedAtAction(nameof(GetTaxRule), new { id = result.Value }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> UpdateTaxRule(Guid id, UpdateTaxRuleRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateTaxRuleCommand(
            id,
            request.CountryCode,
            request.Region,
            request.City,
            request.RateValue,
            request.RateType,
            request.Name,
            request.EffectiveFrom,
            request.EffectiveTo);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == TaxRuleErrors.NotFound)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    detail: result.Error.Name,
                    title: result.Error.Code);
            }

            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> DeactivateTaxRule(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeactivateTaxRuleCommand(id);
        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == TaxRuleErrors.NotFound)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    detail: result.Error.Name,
                    title: result.Error.Code);
            }

            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return NoContent();
    }
}
