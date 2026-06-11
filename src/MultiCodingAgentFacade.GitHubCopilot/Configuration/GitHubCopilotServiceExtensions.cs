using MultiCodingAgentFacade.GitHubCopilot.Abstractions;
using MultiCodingAgentFacade.GitHubCopilot.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MultiCodingAgentFacade.GitHubCopilot.Configuration;

public static class GitHubCopilotServiceExtensions
{
    public static IServiceCollection AddGitHubCopilotAgentRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(GitHubCopilotOptions.SectionName).Get<GitHubCopilotOptions>() ?? new GitHubCopilotOptions();
        services.AddSingleton(options);
        services.AddSingleton(static sp =>
        {
            var options = sp.GetRequiredService<GitHubCopilotOptions>();
            var logger = sp.GetService<ILogger<GitHubCopilotSdkWrapper>>() ?? NullLogger<GitHubCopilotSdkWrapper>.Instance;
            return new GitHubCopilotSdkWrapper(options, logger);
        });
        services.AddSingleton<ICopilotSdkWrapper>(static sp => sp.GetRequiredService<GitHubCopilotSdkWrapper>());
        services.AddSingleton<GitHubCopilotAgentClient>();
        return services;
    }
}
