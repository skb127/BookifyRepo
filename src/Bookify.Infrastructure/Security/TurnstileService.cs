using System.Net.Http.Json;
using Bookify.Application.Abstractions.Security;
using Bookify.Domain.Abstractions;
using Bookify.Infrastructure.Security.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookify.Infrastructure.Security;

internal sealed class TurnstileService : ITurnstileValidator
{
    private readonly TurnstileOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<TurnstileService> _logger;
    
    private static readonly Error ValidationFailed = new("Turnstile.ValidationFailed", "Turnstile validation failed");
    private static readonly Error InvalidToken = new("Turnstile.InvalidToken", "Turnstile token is invalid");
    
    private const string SiteVerifyEndpoint = "siteverify";

    public TurnstileService(IOptions<TurnstileOptions> options, 
        HttpClient httpClient,
        ILogger<TurnstileService> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result> Validate(string token, CancellationToken cancellationToken)
    {
        try
        {
            var turnstileRequestParameters = new KeyValuePair<string, string>[]
            {
                new("response", token),
                new("secret", _options.SecretKey)
            };
        
            using var turnstileRequestContent = new FormUrlEncodedContent(turnstileRequestParameters);

            HttpResponseMessage response = await _httpClient.PostAsync(new Uri(SiteVerifyEndpoint, UriKind.Relative),
                turnstileRequestContent, cancellationToken);
        
            response.EnsureSuccessStatusCode();

            TurnstileResponse? turnstileResponse = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken);

            if (turnstileResponse is null)
            {
                _logger.LogError("Unable to read turnstile json response");
                return Result.Failure(ValidationFailed);
            }

            if (turnstileResponse.Success)
            {
                return Result.Success();
            }

            string reason = turnstileResponse.ErrorCodes?.FirstOrDefault() ?? "Unknown error";
            _logger.LogError("Validation error failed, error code: {ErrorCode}", reason);
            return Result.Failure(InvalidToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Turnstile internal error: {ErrorMessage}", ex.Message);
            return Result.Failure(ValidationFailed);
        }
    }
}
