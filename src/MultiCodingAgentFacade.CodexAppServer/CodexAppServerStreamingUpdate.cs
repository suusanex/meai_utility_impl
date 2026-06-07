namespace MultiCodingAgentFacade.CodexAppServer;

public enum CodexAppServerStreamingUpdateKind
{
    Delta,
    Completed,
}

public sealed record CodexAppServerStreamingUpdate(
    CodexAppServerStreamingUpdateKind Kind,
    string? TextDelta = null,
    string? FinalText = null);
