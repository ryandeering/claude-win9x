using System.Text.Json.Serialization;

namespace ClaudeWin9xServer.Models.Requests;

public record FileWriteRequest
{
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }
}
