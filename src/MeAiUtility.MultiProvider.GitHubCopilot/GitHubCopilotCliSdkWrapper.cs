using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;

namespace MeAiUtility.MultiProvider.GitHubCopilot;

[Obsolete("Use GitHubCopilotSdkWrapper or AddGitHubCopilotSdkWrapper(). This compatibility type now delegates to the SDK-based implementation.")]
public sealed class GitHubCopilotCliSdkWrapper(GitHubCopilotSdkWrapper inner) : ICopilotSdkWrapper, IDisposable, IAsyncDisposable
{
    public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
        => inner.ListModelsAsync(cancellationToken);

    public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
        => inner.SendAsync(prompt, config, cancellationToken);

    public void Dispose()
        => inner.Dispose();

    public ValueTask DisposeAsync()
        => inner.DisposeAsync();
}
