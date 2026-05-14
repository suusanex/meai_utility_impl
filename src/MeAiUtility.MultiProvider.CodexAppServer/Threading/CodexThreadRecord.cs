namespace MeAiUtility.MultiProvider.CodexAppServer.Threading;

internal sealed record CodexThreadRecord(
    string ThreadKey,
    string ThreadId,
    string? ThreadName,
    string? WorkingDirectory,
    string? ModelId,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt);
