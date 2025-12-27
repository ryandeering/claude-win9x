using ClaudeWin9xServer.Models.Responses;

namespace ClaudeWin9xServer.Services.Interfaces;

public interface IApprovalService
{
    Task<bool> RequestApprovalAsync(string sessionId, string toolName, string toolInput, TimeSpan timeout, CancellationToken cancellationToken = default);
    ToolApprovalRequest? PollPendingApproval(string sessionId);
    bool SubmitResponse(string approvalId, bool approved);
}
