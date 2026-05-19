using System.Text.Json.Serialization;

namespace Bookify.Infrastructure.Security.Models;

public sealed class TurnstileResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }
    
    [JsonPropertyName("challenge_ts")]
    public DateTimeOffset ChallengeTs { get; init; }

    [JsonPropertyName("hostname")] 
    public string Hostname { get; init; } = "";

    [JsonPropertyName("error-codes")]
    public string[]? ErrorCodes { get; init; }
    
    [JsonPropertyName("action")]
    public string Action { get; init; } = "";
    
    [JsonPropertyName("cdata")]
    public string CData { get; init; } = "";
    
    [JsonPropertyName("tokenId")]
    public string TokenId { get; init; } = "";

    [JsonPropertyName("metadata")]
    public Dictionary<string, System.Text.Json.JsonElement> Metadata { get; init; } = new();
}
