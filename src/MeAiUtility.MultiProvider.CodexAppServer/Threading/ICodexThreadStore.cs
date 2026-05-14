namespace MeAiUtility.MultiProvider.CodexAppServer.Threading;

internal interface ICodexThreadStore
{
    Task<CodexThreadRecord?> TryGetByKeyAsync(string threadKey, string? threadStorePath, CancellationToken cancellationToken);
    Task<IReadOnlyList<CodexThreadRecord>> ListAsync(string? threadStorePath, CancellationToken cancellationToken);
    Task SaveAsync(CodexThreadRecord record, string? threadStorePath, CancellationToken cancellationToken);
}
