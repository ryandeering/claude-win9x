using ClaudeWin9xServer.Models.Responses;

namespace ClaudeWin9xServer.Services.Interfaces;

public interface ISessionService
{
    (string SessionId, string Status) StartSession(string? workingDirectory, string? windowsVersion);
    Task<bool> SendInput(string sessionId, string text);
    (string Output, string Status)? GetOutput(string sessionId);
    bool StopSession(string sessionId);
    SessionInfo[] ListSessions();
    string? GetWorkingDirectory(string sessionId);
}
