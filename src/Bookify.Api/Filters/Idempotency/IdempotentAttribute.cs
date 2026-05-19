using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;

namespace Bookify.Api.Filters.Idempotency;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class IdempotentAttribute(int cacheTimeInMinutes = 60) : Attribute, IAsyncActionFilter
{
    public int CacheTimeInMinutes { get; } = cacheTimeInMinutes;

    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(cacheTimeInMinutes);

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // If no Idempotence-Key header is present, process the request normally (backward compatible)
        if (!context.HttpContext.Request.Headers.TryGetValue(
                "Idempotence-Key", out Microsoft.Extensions.Primitives.StringValues idempotenceKeyValue)
            || !Guid.TryParse(idempotenceKeyValue, out Guid idempotenceKey))
        {
            await next();
            return;
        }

        // Compute hash from ActionArguments instead of raw body since model binding has already occurred
        string bodyHash = ComputeArgumentsHash(context.ActionArguments);

        IDistributedCache cache = context.HttpContext.RequestServices
            .GetRequiredService<IDistributedCache>();

        string cacheKey = $"Idempotent_{idempotenceKey}";
        string? cachedResult = await cache.GetStringAsync(cacheKey);

        // Cache hit — validate body hash before returning the stored response
        if (cachedResult is not null)
        {
            IdempotentResponse cached = JsonSerializer.Deserialize<IdempotentResponse>(cachedResult)!;

            if (cached.BodyHash != bodyHash)
            {
                // Same key, different body — reject to prevent misuse
                context.Result = new UnprocessableEntityObjectResult(
                    "The Idempotence-Key has already been used with a different request body.");
                return;
            }

            context.Result = new ObjectResult(cached.Value) { StatusCode = cached.StatusCode };
            return;
        }

        // Cache miss — execute the handler and cache the response if successful
        ActionExecutedContext executedContext = await next();

        if (executedContext.Result is ObjectResult { StatusCode: >= 200 and < 300 } objectResult)
        {
            int statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
            var response = new IdempotentResponse(statusCode, objectResult.Value, bodyHash);

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(response),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheDuration
                });
        }
    }

    private static string ComputeArgumentsHash(IDictionary<string, object?> arguments)
    {
        var serializableArguments = arguments
            .Where(kvp => kvp.Value is not CancellationToken)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
        string json = JsonSerializer.Serialize(serializableArguments);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
        byte[] hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
