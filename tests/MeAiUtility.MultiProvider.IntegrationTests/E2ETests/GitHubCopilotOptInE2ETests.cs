using MeAiUtility.MultiProvider.Configuration;
using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Configuration;
using MeAiUtility.MultiProvider.Options;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeAiUtility.MultiProvider.IntegrationTests.E2ETests;

public class GitHubCopilotOptInE2ETests
{
    private const string ExecutionOptInEnvironmentVariable = "MEAI_RUN_GITHUB_COPILOT_E2E";
    private const string RequiredModelId = "gpt-5-mini";
    private const int MaxSendCalls = 10;
    private const string LongStructuredPrompt = """
You design Windows desktop UI automation scenarios. Return exactly one JSON object with a top-level "scenario"
property and no markdown. Use only the evidence below. Requirements:

 - preserve bundleId "bundle-20260330-080000000-fabb9d81cf34" and targetProcessName "notepad"
 - set waitForMainWindow to true
 - include one or more executable steps
 - allowed actions: click, double-click, input, key, assert-exists, assert-text-contains
 - selector fields may use only: name, automationId, controlType, className, frameworkId
 - keep the scenario minimal and evidence-based
 Evidence:
 - requestKind: InitialGeneration
 - bundleId: "bundle-20260330-080000000-fabb9d81cf34"
 - targetProcessName: "notepad"
 - intent: "Enter 1234 into Notepad, open Save As, and confirm saving the file."
 - recordedSteps:
 -
  1. action=click, description="Clicked the File menu in Notepad.", windowTitle="Untitled - Notepad", windowClassName="Notepad",
target=name="File", controlType="MenuItem", className="MenuItem", frameworkId="Win32", parentChain=["MenuBar[Application]"],
focused=name="File", controlType="MenuItem", className="MenuItem", frameworkId="Win32", parentChain=["MenuBar[Application]"],
nearby=[name="Text Editor", controlType="Document", className="RichEditD2DPT", frameworkId="Win32", parentChain=["Window[Untitled -
Notepad]"]]
 -
  1. action=input, description="Entered 1234 into the Notepad editor.", windowTitle="Untitled - Notepad", windowClassName="Notepad",
textInput="1234", target=name="Text Editor", controlType="Document", className="RichEditD2DPT", frameworkId="Win32",
parentChain=["Window[Untitled - Notepad]"], focused=name="Text Editor", controlType="Document", className="RichEditD2DPT",
frameworkId="Win32", parentChain=["Window[Untitled - Notepad]"], nearby=[name="File", controlType="MenuItem", className="MenuItem",
frameworkId="Win32", parentChain=["MenuBar[Application]"]]
 -
  1. action=key, description="Pressed Ctrl+Shift+S to open the Save As dialog.", windowTitle="Untitled - Notepad",
windowClassName="Notepad", focused=name="Text Editor", controlType="Document", className="RichEditD2DPT", frameworkId="Win32",
parentChain=["Window[Untitled - Notepad]"], nearby=[name="File", controlType="MenuItem", className="MenuItem", frameworkId="Win32",
parentChain=["MenuBar[Application]"]]
 -
  1. action=key, description="Pressed Enter in the Save As dialog to confirm the save.", windowTitle="Save As", windowClassName="#32770",
 target=name="Save", controlType="Button", className="Button", frameworkId="Win32", parentChain=["Window[Save As]"], focused=name="Save",
 controlType="Button", className="Button", frameworkId="Win32", parentChain=["Window[Save As]"], nearby=[name="Save",
controlType="Button", className="Button", frameworkId="Win32", parentChain=["Window[Save As]"]]
""";

    [Test]
    [Explicit("Opt-in only. Set MEAI_RUN_GITHUB_COPILOT_E2E=1 and run this test explicitly when GitHub Copilot E2E behavior needs debugging.")]
    [Category("GitHubCopilotE2E")]
    [NonParallelizable]
    public async Task CommonChatInterface_ReachesRealCopilot_WithFixedGpt5MiniModel()
    {
        RequireExecutionOptIn();

        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMultiProviderChat(configuration);
        services.AddGitHubCopilotProvider(configuration);
        services.AddGitHubCopilotSdkWrapper();
        services.AddSingleton<RecordingForwardingCopilotSdkWrapper>();
        services.AddSingleton<ICopilotSdkWrapper>(sp => sp.GetRequiredService<RecordingForwardingCopilotSdkWrapper>());

        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<IChatClient>();
        var wrapper = provider.GetRequiredService<RecordingForwardingCopilotSdkWrapper>();
        var catalog = provider.GetRequiredService<ICopilotModelCatalog>();

        var models = await catalog.ListModelsAsync();

        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.execution"] = new ConversationExecutionOptions
        {
            ModelId = RequiredModelId,
        };

        var response = await chatClient.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User, "Return just OK."),
        ], options);

        TestContext.Out.WriteLine($"Copilot response: {response.Text}");

        Assert.That(provider.GetRequiredService<GitHubCopilotSdkWrapper>(), Is.Not.Null);
        Assert.That(models.Select(static model => model.ModelId), Contains.Item(RequiredModelId));
        Assert.That(response.Messages, Is.Not.Empty);
        Assert.That(response.Messages[^1].Role, Is.EqualTo(ChatRole.Assistant));
        Assert.That(response.Text, Is.Not.Null.And.Not.Empty);
        Assert.That(wrapper.SendCallCount, Is.EqualTo(1));
        Assert.That(wrapper.SendCallCount, Is.LessThanOrEqualTo(MaxSendCalls));
        Assert.That(wrapper.LastPrompt, Is.EqualTo("User: Return just OK."));
        Assert.That(wrapper.LastConfig, Is.Not.Null);
        Assert.That(wrapper.LastConfig!.ModelId, Is.EqualTo(RequiredModelId));
    }

    [Test]
    [Explicit("Opt-in only. Set MEAI_RUN_GITHUB_COPILOT_E2E=1 and run this test explicitly when GitHub Copilot model validation needs debugging.")]
    [Category("GitHubCopilotE2E")]
    [NonParallelizable]
    public void CommonChatInterface_RejectsInvalidCliModelId()
    {
        RequireExecutionOptIn();

        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMultiProviderChat(configuration);
        services.AddGitHubCopilotProvider(configuration);
        services.AddGitHubCopilotSdkWrapper();
        services.AddSingleton<RecordingForwardingCopilotSdkWrapper>();
        services.AddSingleton<ICopilotSdkWrapper>(sp => sp.GetRequiredService<RecordingForwardingCopilotSdkWrapper>());

        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<IChatClient>();
        var wrapper = provider.GetRequiredService<RecordingForwardingCopilotSdkWrapper>();

        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.execution"] = new ConversationExecutionOptions
        {
            ModelId = "GPT-5 mini",
        };

        var ex = Assert.ThrowsAsync<MeAiUtility.MultiProvider.Exceptions.InvalidRequestException>(
            async () => await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Return just OK.")], options));

        Assert.That(ex!.Message, Does.Contain("Unknown GitHub Copilot model id 'GPT-5 mini'"));
        Assert.That(ex.Message, Does.Contain(RequiredModelId));
        Assert.That(wrapper.SendCallCount, Is.EqualTo(0));
    }

    [Test]
    [Explicit("Opt-in only. Set MEAI_RUN_GITHUB_COPILOT_E2E=1 and run this test explicitly when long structured prompts need Copilot E2E verification.")]
    [Category("GitHubCopilotE2E")]
    [NonParallelizable]
    public async Task CommonChatInterface_ReachesRealCopilot_WithLongStructuredPrompt()
    {
        RequireExecutionOptIn();

        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMultiProviderChat(configuration);
        services.AddGitHubCopilotProvider(configuration);
        services.AddGitHubCopilotSdkWrapper();
        services.AddSingleton<RecordingForwardingCopilotSdkWrapper>();
        services.AddSingleton<ICopilotSdkWrapper>(sp => sp.GetRequiredService<RecordingForwardingCopilotSdkWrapper>());

        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<IChatClient>();
        var wrapper = provider.GetRequiredService<RecordingForwardingCopilotSdkWrapper>();

        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.execution"] = new ConversationExecutionOptions
        {
            ModelId = RequiredModelId,
        };

        var response = await chatClient.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User, LongStructuredPrompt),
        ], options);

        var responseText = response.Text;
        var responseTextLength = responseText?.Length ?? -1;
        TestContext.Out.WriteLine($"Copilot long prompt response length: {responseTextLength}");
        TestContext.Out.WriteLine($"Copilot long prompt response: {responseText ?? "<null>"}");

        Assert.That(response.Messages, Is.Not.Empty);
        Assert.That(response.Messages[^1].Role, Is.EqualTo(ChatRole.Assistant));
        Assert.That(responseText, Is.Not.Null.And.Not.Empty);
        Assert.That(wrapper.SendCallCount, Is.EqualTo(1));
        Assert.That(wrapper.SendCallCount, Is.LessThanOrEqualTo(MaxSendCalls));
        Assert.That(wrapper.LastPrompt, Is.EqualTo($"User: {LongStructuredPrompt}"));
        Assert.That(wrapper.LastConfig, Is.Not.Null);
        Assert.That(wrapper.LastConfig!.ModelId, Is.EqualTo(RequiredModelId));

        using var document = JsonDocument.Parse(responseText!);
        Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(document.RootElement.TryGetProperty("scenario", out var scenarioElement), Is.True);
        Assert.That(scenarioElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
    }

    private static void RequireExecutionOptIn()
    {
        if (string.Equals(Environment.GetEnvironmentVariable(ExecutionOptInEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            return;
        }

        Assert.Ignore($"Set {ExecutionOptInEnvironmentVariable}=1 to enable this opt-in GitHub Copilot E2E test.");
    }

    private static IConfiguration BuildConfiguration()
    {
        var githubToken = Environment.GetEnvironmentVariable("MEAI_GITHUB_COPILOT_GITHUB_TOKEN");
        var useLoggedInUser = GetOptionalBooleanEnvironmentVariable("MEAI_GITHUB_COPILOT_USE_LOGGED_IN_USER")
            ?? string.IsNullOrWhiteSpace(githubToken);

        var settings = new Dictionary<string, string?>
        {
            ["MultiProvider:Provider"] = "GitHubCopilot",
            ["MultiProvider:GitHubCopilot:ModelId"] = RequiredModelId,
            ["MultiProvider:GitHubCopilot:UseLoggedInUser"] = useLoggedInUser.ToString(),
            ["MultiProvider:GitHubCopilot:TimeoutSeconds"] = GetOptionalInt32EnvironmentVariable("MEAI_GITHUB_COPILOT_TIMEOUT_SECONDS")?.ToString() ?? "120",
        };

        AddIfPresent(settings, "MultiProvider:GitHubCopilot:CliPath", "MEAI_GITHUB_COPILOT_CLI_PATH");
        AddIfPresent(settings, "MultiProvider:GitHubCopilot:ConfigDir", "MEAI_GITHUB_COPILOT_CONFIG_DIR");
        AddIfPresent(settings, "MultiProvider:GitHubCopilot:WorkingDirectory", "MEAI_GITHUB_COPILOT_WORKING_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            settings["MultiProvider:GitHubCopilot:GitHubToken"] = githubToken;
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

    private sealed class RecordingForwardingCopilotSdkWrapper(GitHubCopilotSdkWrapper inner) : ICopilotSdkWrapper
    {
        public string? LastPrompt { get; private set; }
        public CopilotSessionConfig? LastConfig { get; private set; }
        public int SendCallCount { get; private set; }

        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => inner.ListModelsAsync(cancellationToken);

        public async Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
        {
            if (SendCallCount >= MaxSendCalls)
            {
                throw new AssertionException($"Copilot SendAsync call count exceeded the allowed limit of {MaxSendCalls}.");
            }

            SendCallCount++;
            LastPrompt = prompt;
            LastConfig = CloneConfig(config);
            return await inner.SendAsync(prompt, config, cancellationToken);
        }

        private static CopilotSessionConfig CloneConfig(CopilotSessionConfig source)
        {
            var clone = new CopilotSessionConfig
            {
                ModelId = source.ModelId,
                ReasoningEffort = source.ReasoningEffort,
                Streaming = source.Streaming,
                ProviderOverride = source.ProviderOverride,
            };

            foreach (var entry in source.AdvancedOptions)
            {
                clone.AdvancedOptions[entry.Key] = entry.Value;
            }

            return clone;
        }
    }
}


