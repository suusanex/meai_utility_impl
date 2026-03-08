using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Configuration;

public static class GitHubCopilotServiceExtensions
{
    public static IServiceCollection AddGitHubCopilotProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var opts = configuration.GetSection("MultiProvider:GitHubCopilot").Get<GitHubCopilotProviderOptions>() ?? new GitHubCopilotProviderOptions();
        services.AddSingleton(opts);
        services.AddSingleton<ICopilotSdkWrapper, DefaultCopilotSdkWrapper>();
        services.AddSingleton<CopilotClientHost>();
        services.AddSingleton<GitHubCopilotChatClient>();
        services.AddSingleton<GitHubCopilotEmbeddingAdapter>();
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp => sp.GetRequiredService<GitHubCopilotEmbeddingAdapter>());
        return services;
    }

    private sealed class DefaultCopilotSdkWrapper : ICopilotSdkWrapper
    {
        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([new CopilotModelInfo("gpt-5", true), new CopilotModelInfo("gpt-4.1", false)]);

        public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
            => Task.FromResult($"Copilot response ({config.ModelId ?? "gpt-5"})");
    }
}
