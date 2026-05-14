namespace MeAiUtility.MultiProvider.CodexAppServer.Threading;

public interface ICodexThreadRegistry
{
    Task<IReadOnlyList<CodexThreadDescriptor>> ListAsync(string? threadStorePath = null, CancellationToken cancellationToken = default);
    Task<CodexThreadDescriptor?> TryGetByThreadKeyAsync(string threadKey, string? threadStorePath = null, CancellationToken cancellationToken = default);
}
