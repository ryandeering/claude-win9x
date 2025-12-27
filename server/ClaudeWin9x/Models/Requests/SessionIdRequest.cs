using System.Text.Json.Serialization;

namespace ClaudeWin9xServer.Models.Requests;

public record SessionIdRequest
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }
}
