namespace ClaudeWin9xServer.Models.Responses;

public record SessionsListResponse
{
    public required SessionInfo[] Sessions { get; init; }
}
