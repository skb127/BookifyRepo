using System.Text.Json.Serialization;

namespace Bookify.Api.Filters.Idempotency;

internal sealed class IdempotentResponse
{
    [JsonConstructor]
    public IdempotentResponse(int statusCode, object? value, string bodyHash)
    {
        StatusCode = statusCode;
        Value = value;
        BodyHash = bodyHash;
    }

    public int StatusCode { get; }
    public object? Value { get; }
    public string BodyHash { get; }
}
