using Bookify.Application.Abstractions.Security;
using Bookify.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

namespace Bookify.Api.Filters.Turnstile;

internal sealed class TurnstileFilter : IAsyncActionFilter
{
    private readonly ITurnstileValidator _turnstileValidator;
    
    public TurnstileFilter(ITurnstileValidator turnstileValidator) =>
        _turnstileValidator = turnstileValidator;
    
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1. Check if the X-Turnstile-Token header is present
        if (!context.HttpContext.Request.Headers.TryGetValue("X-Turnstile-Token", out StringValues tokenValues))
        {
            context.Result = new BadRequestObjectResult("Missing Turnstile token");
            return;
        }

        // 2. Make sure the token is not empty
        string? token = tokenValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            context.Result = new BadRequestObjectResult("Empty Turnstile token");
            return;
        }
        
        Result result = await _turnstileValidator.Validate(token, context.HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            context.Result = new BadRequestObjectResult("Invalid Turnstile token");
            return;
        }
        
        await next();
    }
}
