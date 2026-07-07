using Bookify.Application.Abstractions.Security;
using Bookify.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

namespace Bookify.Api.Filters.Turnstile;

internal sealed class TurnstileFilter : IAsyncActionFilter
{
    private readonly ITurnstileValidator _turnstileValidator;
    private readonly ILogger<TurnstileFilter> _logger;

    public TurnstileFilter(
        ITurnstileValidator turnstileValidator,
        ILogger<TurnstileFilter> logger)
    {
        _turnstileValidator = turnstileValidator;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1. Check if the X-Turnstile-Token header is present
        if (!context.HttpContext.Request.Headers.TryGetValue("X-Turnstile-Token", out StringValues tokenValues))
        {
            _logger.LogWarning("Turnstile validation failed: Missing Turnstile token");
            context.Result = CreateProblemResult("Missing Turnstile token");
            return;
        }

        // 2. Make sure the token is not empty
        string? token = tokenValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Turnstile validation failed: Empty Turnstile token");
            context.Result = CreateProblemResult("Empty Turnstile token");
            return;
        }

        Result result = await _turnstileValidator.Validate(token, context.HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            _logger.LogWarning("Turnstile validation failed: Invalid Turnstile token. Error: {Error}", result.Error);
            context.Result = CreateProblemResult("Invalid Turnstile token");
            return;
        }

        await next();
    }

    private static ObjectResult CreateProblemResult(string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Type = "TurnstileError",
            Title = "Turnstile validation failed",
            Detail = detail
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }
}
