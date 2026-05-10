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

    [Test]
    [Explicit("Opt-in only. Set MEAI_RUN_CODEX_APP_SERVER_E2E=1 and run this test explicitly when Codex App Server E2E behavior needs debugging.")]
    [Category("CodexAppServerE2E")]
    [NonParallelizable]
    public async Task CommonChatInterface_ReachesRealCodexAppServer_AndDetectsUnexpectedProcessExit()
    {
        RequireExecutionOptIn();

        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMultiProviderChat(configuration);
        services.AddCodexAppServer(configuration);

        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<IChatClient>();

        try
        {
            var response = await chatClient.GetResponseAsync(
            [
                new ChatMessage(ChatRole.User, "Reply with exactly: OK"),
            ]);

            TestContext.Out.WriteLine($"Codex response: {response.Text}");
            Assert.That(response.Messages, Is.Not.Empty);
            Assert.That(response.Messages[^1].Role, Is.EqualTo(ChatRole.Assistant));
            Assert.That(response.Text, Is.Not.Null.And.Not.Empty);
        }
        catch (ProviderException ex)
            when (ex.InnerException is CodexProcessExitedException)
        {
            TestContext.Out.WriteLine(ex.ToString());
            Assert.Fail($"Detected reproducible Codex App Server process termination: '{CodexProcessExitedException.MessageText}'");
        }
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
        AddIfPresent(settings, "MultiProvider:CodexAppServer:WorkingDirectory", "MEAI_CODEX_APP_SERVER_WORKING_DIRECTORY");

        var networkAccess = GetOptionalBooleanEnvironmentVariable("MEAI_CODEX_APP_SERVER_NETWORK_ACCESS");
        if (networkAccess.HasValue)
        {
            settings["MultiProvider:CodexAppServer:NetworkAccess"] = networkAccess.Value.ToString();
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
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
}
