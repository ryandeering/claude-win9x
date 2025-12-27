namespace ClaudeWin9xServer.Infrastructure;

internal static class IdGenerator
{
    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
