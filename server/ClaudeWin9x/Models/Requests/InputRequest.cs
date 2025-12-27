using System.Text.Json.Serialization;

namespace ClaudeWin9xServer.Models.Requests;

public record InputRequest
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }
}
