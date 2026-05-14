namespace MeAiUtility.MultiProvider.CodexAppServer.Threading;

public enum CodexThreadReusePolicy
{
    AlwaysNew,
    ReuseByThreadId,
    ReuseOrCreateByKey,
}
