namespace MultiCodingAgentFacade.CodexAppServer.Threading;

public enum CodexThreadReusePolicy
{
    AlwaysNew,
    ReuseByThreadId,
    ReuseOrCreateByKey,
}
