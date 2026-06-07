namespace MultiCodingAgentFacade.CodexAppServer.Threading;

public sealed record CodexThreadRecord(
    string ThreadKey,
    string ThreadId,
    string? ThreadName,
    string? WorkingDirectory,
    string? ModelId,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt);
