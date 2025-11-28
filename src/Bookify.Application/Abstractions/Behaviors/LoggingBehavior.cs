using Bookify.Domain.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Bookify.Application.Abstractions.Behaviors;

public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    //where TRequest : IBaseCommand // this is to limit logging behavior to commands only
    where TRequest : IBaseRequest // this is to limit logging behavior to commands and queries, our commands and queries implement IBaseRequest under the hood
    where TResponse : Result // this is to limit logging behavior to responses that are of type Result, this allows us to check for success or failure in the response
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => 
        _logger = logger;

    public async Task<TResponse> Handle(TRequest request, 
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string name = request.GetType().Name;

        try
        {
            _logger.LogInformation("Executing request {Request}", name);

            TResponse result = await next(cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Request {Request} processed successfully", name);
            }
            else
            {
                //_logger.LogInformation("Request {Request} processed with {@Error}", name, result.Error);
                using (LogContext.PushProperty("Error", result.Error, true)) // DestructureObjects, tells Serilog to serialize the object Error into a JSON object when writing structured logs
                {
                    _logger.LogError("Request {Request} processed with error", name);
                }
            }

            return result;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Request {Request} processing failed", name);

            throw;
        }
    }
}
