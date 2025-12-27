using System.Text.Json.Serialization;

namespace ClaudeWin9xServer.Models.Responses;

public record ErrorResponse
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }
}
