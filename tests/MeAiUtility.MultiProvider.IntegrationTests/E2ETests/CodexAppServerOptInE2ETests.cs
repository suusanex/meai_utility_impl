using MeAiUtility.MultiProvider.CodexAppServer;
using MeAiUtility.MultiProvider.CodexAppServer.Configuration;
using MeAiUtility.MultiProvider.Configuration;
using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeAiUtility.MultiProvider.IntegrationTests.E2ETests;

public class CodexAppServerOptInE2ETests
{
    private const string ExecutionOptInEnvironmentVariable = "MEAI_RUN_CODEX_APP_SERVER_E2E";
    private const string DefaultReportedModelId = "gpt-5.4";
    private const string ReportedLikeSystemPrompt = "You are a concise assistant. Follow the user request exactly.";
    private const string ReportedLikeUserPrompt = "Reply with exactly: OK";

    [Test]
    [Explicit("Opt-in only. Set MEAI_RUN_CODEX_APP_SERVER_E2E=1 and run this test explicitly when Codex App Server E2E behavior needs debugging.")]
    [Category("CodexAppServerE2E")]
    [NonParallelizable]
    public async Task CommonChatInterface_ReachesRealCodexAppServer_AndDetectsUnexpectedProcessExit()
    {
        RequireExecutionOptIn();

        var configuration = BuildConfiguration();
        var response = await ExecuteChatAsync(
            configuration,
            [new ChatMessage(ChatRole.User, "Reply with exactly: OK")]);

        TestContext.Out.WriteLine($"Codex response: {response.Text}");
        Assert.That(response.Messages, Is.Not.Empty);
        Assert.That(response.Messages[^1].Role, Is.EqualTo(ChatRole.Assistant));
        Assert.That(response.Text, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [Explicit("Opt-in only. Set MEAI_RUN_CODEX_APP_SERVER_E2E=1 and run this test explicitly when reported Codex App Server option combinations need debugging." )]
    [Category("CodexAppServerE2E")]
    [NonParallelizable]
    public async Task CommonChatInterface_ReachesRealCodexAppServer_WithReportedLikePromptAndOptions()
    {
        RequireExecutionOptIn();

        var configuration = BuildConfiguration();
        var options = CreateReportedLikeOptions(configuration);
        var response = await ExecuteChatAsync(
            configuration,
            [
                new ChatMessage(ChatRole.System, ReportedLikeSystemPrompt),
                new ChatMessage(ChatRole.User, ReportedLikeUserPrompt),
            ],
            options,
            "reported-like");

        TestContext.Out.WriteLine($"Codex reported-like response: {response.Text}");
        Assert.That(response.Messages, Is.Not.Empty);
        Assert.That(response.Messages[^1].Role, Is.EqualTo(ChatRole.Assistant));
        Assert.That(response.Text, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Text, Does.Contain("OK"));
    }

    private static void RequireExecutionOptIn()
    {
        if (string.Equals(Environment.GetEnvironmentVariable(ExecutionOptInEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            return;
        }

        Assert.Ignore($"Set {ExecutionOptInEnvironmentVariable}=1 to enable this opt-in Codex App Server E2E test.");
    }

    private static IConfiguration BuildConfiguration()
    {
        var timeoutSeconds = GetOptionalInt32EnvironmentVariable("MEAI_CODEX_APP_SERVER_TIMEOUT_SECONDS")?.ToString() ?? "120";
        var autoApprove = GetOptionalBooleanEnvironmentVariable("MEAI_CODEX_APP_SERVER_AUTO_APPROVE") ?? true;
        var captureEvents = GetOptionalBooleanEnvironmentVariable("MEAI_CODEX_APP_SERVER_CAPTURE_EVENTS_FOR_DIAGNOSTICS") ?? false;

        var settings = new Dictionary<string, string?>
        {
            ["MultiProvider:Provider"] = "CodexAppServer",
            ["MultiProvider:CodexAppServer:Transport"] = "stdio",
            ["MultiProvider:CodexAppServer:ApprovalPolicy"] = Environment.GetEnvironmentVariable("MEAI_CODEX_APP_SERVER_APPROVAL_POLICY") ?? "never",
            ["MultiProvider:CodexAppServer:SandboxMode"] = Environment.GetEnvironmentVariable("MEAI_CODEX_APP_SERVER_SANDBOX_MODE") ?? "workspace-write",
            ["MultiProvider:CodexAppServer:TimeoutSeconds"] = timeoutSeconds,
            ["MultiProvider:CodexAppServer:AutoApprove"] = autoApprove.ToString(),
            ["MultiProvider:CodexAppServer:CaptureEventsForDiagnostics"] = captureEvents.ToString(),
        };

        AddIfPresent(settings, "MultiProvider:CodexAppServer:CodexCommand", "MEAI_CODEX_APP_SERVER_COMMAND");
        AddIfPresent(settings, "MultiProvider:CodexAppServer:ModelId", "MEAI_CODEX_APP_SERVER_MODEL_ID");
        AddIfPresent(settings, "MultiProvider:CodexAppServer:ReasoningEffort", "MEAI_CODEX_APP_SERVER_REASONING_EFFORT");
        AddIfPresent(settings, "MultiProvider:CodexAppServer:WorkingDirectory", "MEAI_CODEX_APP_SERVER_WORKING_DIRECTORY");
        AddIfPresent(settings, "MultiProvider:CodexAppServer:ServiceName", "MEAI_CODEX_APP_SERVER_SERVICE_NAME");
        AddIfPresent(settings, "MultiProvider:CodexAppServer:Summary", "MEAI_CODEX_APP_SERVER_SUMMARY");
        AddIfPresent(settings, "MultiProvider:CodexAppServer:Personality", "MEAI_CODEX_APP_SERVER_PERSONALITY");

        var networkAccess = GetOptionalBooleanEnvironmentVariable("MEAI_CODEX_APP_SERVER_NETWORK_ACCESS");
        if (networkAccess.HasValue)
        {
            settings["MultiProvider:CodexAppServer:NetworkAccess"] = networkAccess.Value.ToString();
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static async Task<ChatResponse> ExecuteChatAsync(
        IConfiguration configuration,
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null,
        string scenarioName = "minimal")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMultiProviderChat(configuration);
        services.AddCodexAppServer(configuration);

        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<IChatClient>();

        try
        {
            return await chatClient.GetResponseAsync(messages, options);
        }
        catch (ProviderException ex)
            when (ex.InnerException is CodexProcessExitedException)
        {
            TestContext.Out.WriteLine($"Scenario={scenarioName}");
            TestContext.Out.WriteLine($"ModelId={options?.ModelId ?? configuration["MultiProvider:CodexAppServer:ModelId"] ?? "<default>"}");
            TestContext.Out.WriteLine(ex.ToString());
            Assert.Fail($"Detected reproducible Codex App Server process termination in scenario '{scenarioName}': '{CodexProcessExitedException.MessageText}'");
            throw;
        }
    }

    private static ChatOptions CreateReportedLikeOptions(IConfiguration configuration)
    {
        var options = new ChatOptions
        {
            ModelId = Environment.GetEnvironmentVariable("MEAI_CODEX_APP_SERVER_REPORTED_MODEL_ID")
                ?? configuration["MultiProvider:CodexAppServer:ModelId"]
                ?? DefaultReportedModelId,
        };

        var execution = new MeAiUtility.MultiProvider.Options.ConversationExecutionOptions
        {
            ModelId = options.ModelId,
            ReasoningEffort = GetOptionalReasoningEffortEnvironmentVariable("MEAI_CODEX_APP_SERVER_REPORTED_REASONING_EFFORT")
                ?? GetOptionalReasoningEffortEnvironmentVariable("MEAI_CODEX_APP_SERVER_REASONING_EFFORT")
                ?? MeAiUtility.MultiProvider.Options.ReasoningEffortLevel.Low,
            WorkingDirectory = Environment.GetEnvironmentVariable("MEAI_CODEX_APP_SERVER_WORKING_DIRECTORY"),
        };

        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[MeAiUtility.MultiProvider.Options.ConversationExecutionOptions.PropertyName] = execution;
        return options;
    }

    private static void AddIfPresent(IDictionary<string, string?> settings, string configurationKey, string environmentVariableName)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        settings[configurationKey] = value;
    }

    private static bool? GetOptionalBooleanEnvironmentVariable(string environmentVariableName)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new AssertionException($"Environment variable '{environmentVariableName}' must be 'true' or 'false'.");
    }

    private static int? GetOptionalInt32EnvironmentVariable(string environmentVariableName)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        throw new AssertionException($"Environment variable '{environmentVariableName}' must be a positive integer.");
    }

    private static MeAiUtility.MultiProvider.Options.ReasoningEffortLevel? GetOptionalReasoningEffortEnvironmentVariable(string environmentVariableName)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "low" => MeAiUtility.MultiProvider.Options.ReasoningEffortLevel.Low,
            "medium" => MeAiUtility.MultiProvider.Options.ReasoningEffortLevel.Medium,
            "high" => MeAiUtility.MultiProvider.Options.ReasoningEffortLevel.High,
            "xhigh" => MeAiUtility.MultiProvider.Options.ReasoningEffortLevel.XHigh,
            _ => throw new AssertionException($"Environment variable '{environmentVariableName}' must be one of: low, medium, high, xhigh."),
        };
    }
}
