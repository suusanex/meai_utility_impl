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
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var opts = configuration.GetSection("MultiProvider:GitHubCopilot").Get<GitHubCopilotProviderOptions>() ?? new GitHubCopilotProviderOptions();
        services.AddSingleton(opts);
        services.AddSingleton<ICopilotSdkWrapper, DefaultCopilotSdkWrapper>();
        services.AddSingleton<CopilotClientHost>();
        services.AddSingleton<GitHubCopilotChatClient>();
        services.AddSingleton<ICopilotModelCatalog>(sp => sp.GetRequiredService<GitHubCopilotChatClient>());
        services.AddSingleton<GitHubCopilotEmbeddingAdapter>();
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp => sp.GetRequiredService<GitHubCopilotEmbeddingAdapter>());
        return services;
    }

    public static IServiceCollection AddGitHubCopilotSdkWrapper(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<GitHubCopilotSdkWrapper>();
        services.AddSingleton<ICopilotSdkWrapper>(sp => sp.GetRequiredService<GitHubCopilotSdkWrapper>());
        return services;
    }

    [Obsolete("Use AddGitHubCopilotSdkWrapper(). This compatibility registration now resolves the SDK-based wrapper.")]
    public static IServiceCollection AddGitHubCopilotCliSdkWrapper(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGitHubCopilotSdkWrapper();
        services.AddSingleton<GitHubCopilotCliSdkWrapper>();
        return services;
    }

    private sealed class DefaultCopilotSdkWrapper : ICopilotSdkWrapper
    {
        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([new CopilotModelInfo("gpt-5-mini", false), new CopilotModelInfo("gpt-5", true), new CopilotModelInfo("gpt-4.1", false)]);

        public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
            => Task.FromResult($"Copilot response ({config.ModelId ?? "gpt-5"})");
    }
}
