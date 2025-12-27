using ClaudeWin9xServer.Models.Requests;
using ClaudeWin9xServer.Models.Responses;

namespace ClaudeWin9xServer.Services.Interfaces;

public interface ICommandService
{
    Task<CommandResult?> QueueCommandAsync(string command, string? workingDirectory, string? sessionId = null, CancellationToken cancellationToken = default);
    CommandRequest? PollPendingCommand();
    void SubmitResult(CommandResult result);
    CommandResult? GetCommandStatus(string commandId);
    bool IsPending(string commandId);
    string? GetPendingStatus(string commandId);
}
