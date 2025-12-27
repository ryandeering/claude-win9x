using System.Text.Json.Serialization;

namespace ClaudeWin9xServer.Models.Requests;

public record ApprovalResponse
{
    [JsonPropertyName("approval_id")]
    public string? ApprovalId { get; init; }

    [JsonPropertyName("approved")]
    public bool Approved { get; init; }
}
