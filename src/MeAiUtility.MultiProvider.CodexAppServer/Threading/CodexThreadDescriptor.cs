namespace MeAiUtility.MultiProvider.CodexAppServer.Threading;

public sealed record CodexThreadDescriptor(
    string ThreadKey,
    string ThreadId,
    string? ThreadName,
    string? WorkingDirectory,
    string? ModelId,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt);
