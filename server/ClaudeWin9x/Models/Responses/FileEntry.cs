using System.Text.Json.Serialization;

namespace ClaudeWin9xServer.Models.Responses;

public record FileEntry
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("size")]
    public required long Size { get; init; }
}
