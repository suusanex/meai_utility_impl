using System.Text.Json;
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

    [OptInIntegrationFact(
        CodexAppServerOptInEnvironmentVariable,
        "Codex App Server long prompt production smoke is disabled. Set MCAF_CODEX_APP_SERVER_INTEGRATION=1 to run it with local credentials.")]
    [Trait("Category", "ManualOnly")]
    public async Task CodexAppServerOptInSmokeUsesProductionClientPathWithLongPrompt()
    {
        using var provider = BuildServiceProvider(services => services.AddCodexAppServerAgentRuntime(CreateEmptyConfiguration()));

        Assert.IsType<SystemCodexProcessRunner>(provider.GetRequiredService<ICodexProcessRunner>());
        Assert.IsType<DefaultCodexTransportFactory>(provider.GetRequiredService<ICodexTransportFactory>());

        var client = provider.GetRequiredService<CodexAppServerAgentClient>();
        var longPrompt = CreateFacadeLongPrompt();
        var response = await client.ExecuteTurnAsync(new CodexAppServerTurnRequest
        {
            Prompt = longPrompt,
            TimeoutSeconds = 120,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            ApprovalPolicy = "never",
            SandboxMode = "workspace-write",
            NetworkAccess = false,
            AutoApprove = false,
            CaptureEventsForDiagnostics = true
        });

        Assert.False(string.IsNullOrWhiteSpace(response.RequestId));
        Assert.False(string.IsNullOrWhiteSpace(response.TraceId));
        Assert.Equal("completed", response.Status);
        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }

    [OptInIntegrationFact(
        CodexAppServerOptInEnvironmentVariable,
        "Codex App Server output schema production smoke is disabled. Set MCAF_CODEX_APP_SERVER_INTEGRATION=1 to run it with local credentials.")]
    [Trait("Category", "ManualOnly")]
    public async Task CodexAppServerOptInSmokeUsesOutputSchema()
    {
        using var provider = BuildServiceProvider(services => services.AddCodexAppServerAgentRuntime(CreateEmptyConfiguration()));
        using var outputSchema = JsonDocument.Parse("""
        {
          "title": "CodexAppServerOutputSchemaSmoke",
          "version": "1.0.0",
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "answer": {
              "type": "string",
              "enum": [
                "OK"
              ]
            }
          },
          "required": [
            "answer"
          ]
        }
        """);

        var client = provider.GetRequiredService<CodexAppServerAgentClient>();
        var response = await client.ExecuteTurnAsync(new CodexAppServerTurnRequest
        {
            Prompt = "Return the JSON object that satisfies the provided output schema.",
            TimeoutSeconds = 60,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            ApprovalPolicy = "never",
            SandboxMode = "workspace-write",
            NetworkAccess = false,
            AutoApprove = false,
            OutputSchema = outputSchema.RootElement
        });

        Assert.Equal("completed", response.Status);
        Assert.Contains("OutputSchema=true", response.DiagnosticsSummary);
        Assert.DoesNotContain("CodexAppServerOutputSchemaSmoke", response.DiagnosticsSummary);
        using var responseDocument = JsonDocument.Parse(response.Text);
        var root = responseDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("OK", root.GetProperty("answer").GetString());
        Assert.Single(root.EnumerateObject());
    }

    private static string CreateFacadeLongPrompt()
    {
        return string.Join(
            Environment.NewLine,
            "You review and refine deterministic baseline scenarios using the baselineReview style.",
            "Return exactly one JSON object with fields status, summary, observations, and recommendedActions.",
            "The JSON object must be valid and must not be wrapped in Markdown.",
            "Context package:",
            "  - baselineReview: 実装差分の妥当性を検証する baselineReview ワークフロー",
            "  - mode: review",
            "  - requestKind: evaluate",
            "  - targetProcessName: codex-app-server",
            "  - allowFallback: false",
            "Requirements:",
            "  - Use a single top-level JSON object.",
            "  - Keep Japanese symbols and non-ASCII text as plain text in the prompt and output",
            "  - Include at least one deterministic action item and one validation note",
            "  - Preserve structure and avoid extra commentary",
            "  - The response should reference attached synthetic evidence when available",
            "Process evidence:",
            """{"process":"codex","state":"萓・stable","notes":"非ASCII文字列を含む観測データ"}""",
            new string('x', 12000));
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
