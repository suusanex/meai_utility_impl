using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MultiCodingAgentFacade.CodexAppServer;
using MultiCodingAgentFacade.CodexAppServer.Abstractions;
using MultiCodingAgentFacade.CodexAppServer.Configuration;
using MultiCodingAgentFacade.CodexAppServer.Stdio;
using MultiCodingAgentFacade.CodexAppServer.Threading;
using MultiCodingAgentFacade.GitHubCopilot;
using MultiCodingAgentFacade.GitHubCopilot.Abstractions;
using MultiCodingAgentFacade.GitHubCopilot.Configuration;
using MultiCodingAgentFacade.GitHubCopilot.Options;
using Xunit;

namespace MultiCodingAgentFacade.IntegrationTests;

public sealed class OptInIntegrationSmokeTests
{
    private const string GitHubCopilotOptInEnvironmentVariable = "MCAF_GITHUB_COPILOT_INTEGRATION";
    private const string CodexAppServerOptInEnvironmentVariable = "MCAF_CODEX_APP_SERVER_INTEGRATION";

    [Fact]
    [Trait("Category", "ProductionBinding")]
    public void GitHubCopilotRuntimeRegistrationUsesProductionSdkWrapper()
    {
        using var provider = BuildServiceProvider(services => services.AddGitHubCopilotAgentRuntime(CreateEmptyConfiguration()));

        Assert.IsType<GitHubCopilotSdkWrapper>(provider.GetRequiredService<ICopilotSdkWrapper>());
        Assert.NotNull(provider.GetRequiredService<GitHubCopilotAgentClient>());
    }

    [Fact]
    [Trait("Category", "ProductionBinding")]
    public void CodexAppServerRuntimeRegistrationUsesProductionTransport()
    {
        using var provider = BuildServiceProvider(services => services.AddCodexAppServerAgentRuntime(CreateEmptyConfiguration()));

        Assert.IsType<SystemCodexProcessRunner>(provider.GetRequiredService<ICodexProcessRunner>());
        Assert.IsType<DefaultCodexTransportFactory>(provider.GetRequiredService<ICodexTransportFactory>());
        Assert.Equal("FileCodexThreadStore", provider.GetRequiredService<ICodexThreadStore>().GetType().Name);
        Assert.NotNull(provider.GetRequiredService<CodexAppServerAgentClient>());
    }

    [OptInIntegrationFact(
        GitHubCopilotOptInEnvironmentVariable,
        "GitHub Copilot real runtime smoke is disabled. Set MCAF_GITHUB_COPILOT_INTEGRATION=1 to run it with local credentials.")]
    [Trait("Category", "ManualOnly")]
    public async Task GitHubCopilotOptInSmokeUsesProductionClientPath()
    {
        using var provider = BuildServiceProvider(services => services.AddGitHubCopilotAgentRuntime(CreateEmptyConfiguration()));

        Assert.IsType<GitHubCopilotSdkWrapper>(provider.GetRequiredService<ICopilotSdkWrapper>());

        var client = provider.GetRequiredService<GitHubCopilotAgentClient>();
        var response = await client.SendTurnAsync(new GitHubCopilotAgentRequest
        {
            Prompt = "Reply with the single word OK.",
            TimeoutSeconds = 30,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            PermissionHandling = GitHubCopilotPermissionHandlingMode.DenyAll
        });

        Assert.Equal(GitHubCopilotRuntimeMarker.RuntimeName, response.RuntimeName);
        Assert.NotEmpty(response.RequestId);
        Assert.NotEmpty(response.TraceId);
        Assert.NotNull(response.Text);
    }

    [OptInIntegrationFact(
        CodexAppServerOptInEnvironmentVariable,
        "Codex App Server real runtime smoke is disabled. Set MCAF_CODEX_APP_SERVER_INTEGRATION=1 to run it with local credentials.")]
    [Trait("Category", "ManualOnly")]
    public async Task CodexAppServerOptInSmokeUsesProductionClientPath()
    {
        using var provider = BuildServiceProvider(services => services.AddCodexAppServerAgentRuntime(CreateEmptyConfiguration()));

        Assert.IsType<SystemCodexProcessRunner>(provider.GetRequiredService<ICodexProcessRunner>());
        Assert.IsType<DefaultCodexTransportFactory>(provider.GetRequiredService<ICodexTransportFactory>());

        var client = provider.GetRequiredService<CodexAppServerAgentClient>();
        var response = await client.ExecuteTurnAsync(new CodexAppServerTurnRequest
        {
            Prompt = "Reply with the single word OK.",
            TimeoutSeconds = 30,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            ApprovalPolicy = "never",
            SandboxMode = "workspace-write",
            NetworkAccess = false,
            AutoApprove = false
        });

        Assert.False(string.IsNullOrWhiteSpace(response.RequestId));
        Assert.False(string.IsNullOrWhiteSpace(response.TraceId));
        Assert.NotNull(response.Text);
    }

    private static ServiceProvider BuildServiceProvider(Func<IServiceCollection, IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IConfiguration CreateEmptyConfiguration()
    {
        return new ConfigurationBuilder().Build();
    }

    private static bool IsOptedIn(string? value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class OptInIntegrationFactAttribute : FactAttribute
    {
        public OptInIntegrationFactAttribute(string environmentVariableName, string skipReason)
        {
            if (!IsOptedIn(Environment.GetEnvironmentVariable(environmentVariableName)))
            {
                Skip = skipReason;
            }
        }
    }
}
