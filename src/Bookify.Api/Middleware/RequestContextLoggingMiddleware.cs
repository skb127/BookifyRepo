using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace Bookify.Api.Middleware;

public class RequestContextLoggingMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public RequestContextLoggingMiddleware(RequestDelegate next) => 
        _next = next;

    public Task InvokeAsync(HttpContext httpContext)
    {
        using (LogContext.PushProperty("CorrelationId", GetCorrelationId(httpContext)))
        {
            return _next(httpContext);
        }
    }

    /// <summary>
    /// Gets the correlation ID from the request headers or falls back to the trace identifier.
    /// The correlation ID is used to trace and correlate requests across different services and components, making it easier to diagnose issues and monitor application behavior.
    /// if the CorrelationIdHeaderName is not present in the request headers, the method uses the HttpContext's TraceIdentifier as a fallback.
    /// </summary>
    /// <param name="httpContext"></param>
    /// <returns></returns>
    private static string GetCorrelationId(HttpContext httpContext)
    {
        httpContext.Request.Headers.TryGetValue(CorrelationIdHeaderName, out StringValues correlationId);

        return correlationId.FirstOrDefault() ?? httpContext.TraceIdentifier;
    }
}
