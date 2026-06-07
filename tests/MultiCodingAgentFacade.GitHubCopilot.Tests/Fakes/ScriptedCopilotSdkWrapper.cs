using System.Runtime.CompilerServices;
using MultiCodingAgentFacade.GitHubCopilot.Abstractions;

namespace MultiCodingAgentFacade.GitHubCopilot.Tests.Fakes;

internal sealed class ScriptedCopilotSdkWrapper : ICopilotSdkWrapper
{
    public bool SupportsStreaming { get; set; }
    public IReadOnlyList<CopilotModelInfo> Models { get; set; } = [new("gpt-5", true)];
    public CopilotSessionConfig? LastConfig { get; private set; }
    public string? LastPrompt { get; private set; }
    public string ResponseText { get; set; } = "response";
    public List<CopilotStreamingUpdate> StreamingUpdates { get; } = [];

    public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Models);

    public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
    {
        LastPrompt = prompt;
        LastConfig = config;
        return Task.FromResult(ResponseText);
    }

    public async IAsyncEnumerable<CopilotStreamingUpdate> SendStreamingAsync(
        string prompt,
        CopilotSessionConfig config,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastPrompt = prompt;
        LastConfig = config;

        foreach (var update in StreamingUpdates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return update;
        }
    }
}
