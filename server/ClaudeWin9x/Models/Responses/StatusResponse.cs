using System.Text.Json.Serialization;

namespace ClaudeWin9xServer.Models.Responses;

public record StatusResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }
}
