using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiCodingAgentFacade.CodexAppServer;
using MultiCodingAgentFacade.CodexAppServer.Configuration;
using MultiCodingAgentFacade.CodexAppServer.Threading;
using MultiCodingAgentFacade.Core.Exceptions;
using MultiCodingAgentFacade.Core.Options;
using MultiCodingAgentFacade.GitHubCopilot;
using MultiCodingAgentFacade.GitHubCopilot.Configuration;
using MultiCodingAgentFacade.GitHubCopilot.Options;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    try
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var traceCodexEvents = HasArg(args, "--trace-codex-events") || IsEnabled("MCAF_CODEX_APP_SERVER_TRACE_EVENTS");
        var traceCopilotEvents = HasArg(args, "--trace-copilot-events") || IsEnabled("MCAF_GITHUB_COPILOT_TRACE_EVENTS");
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });

            if (traceCodexEvents)
            {
                builder.AddFilter("MultiCodingAgentFacade.CodexAppServer.CodexRpcSession", LogLevel.Debug);
            }

            if (traceCopilotEvents)
            {
                builder.AddFilter("MultiCodingAgentFacade.GitHubCopilot", LogLevel.Debug);
            }
        });
        services.AddGitHubCopilotAgentRuntime(configuration);
        services.AddCodexAppServerAgentRuntime(configuration);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var copilot = provider.GetRequiredService<GitHubCopilotAgentClient>();
        var codex = provider.GetRequiredService<CodexAppServerAgentClient>();

        if (HasArg(args, "--run-copilot"))
        {
            return await RunCopilotAsync(copilot);
        }

        if (HasArg(args, "--stream-copilot"))
        {
            return await StreamCopilotAsync(copilot);
        }

        if (HasArg(args, "--run-codex"))
        {
            return await RunCodexAsync(codex);
        }

        if (HasArg(args, "--stream-codex"))
        {
            return await StreamCodexAsync(codex);
        }

        PrintDryRun();
        return 0;
    }
    catch (Exception ex)
    {
        Trace.TraceError(ex.ToString());
        Console.Error.WriteLine($"Sample failed: {ex.GetType().Name}: {ex.Message}");
        return 1;
    }
}

static void PrintDryRun()
{
    var copilotRequest = CreateCopilotRequest(streaming: false);
    var codexRequest = CreateCodexRequest();

    Console.WriteLine("MultiCodingAgentFacade sample dry run.");
    Console.WriteLine("Production DI registrations were resolved without starting real runtimes.");
    Console.WriteLine($"Copilot request: ModelId={copilotRequest.ModelId}; PermissionHandling={copilotRequest.PermissionHandling}; WorkingDirectory={copilotRequest.WorkingDirectory}");
    Console.WriteLine($"Codex request: ModelId={codexRequest.ModelId}; ApprovalPolicy={codexRequest.ApprovalPolicy}; SandboxMode={codexRequest.SandboxMode}; WorkingDirectory={codexRequest.WorkingDirectory}");
    Console.WriteLine("Use --run-copilot or --stream-copilot with MCAF_GITHUB_COPILOT_INTEGRATION=1 to execute GitHub Copilot.");
    Console.WriteLine("Use --run-codex or --stream-codex with MCAF_CODEX_APP_SERVER_INTEGRATION=1 to execute Codex App Server.");
    Console.WriteLine("Add --trace-codex-events or set MCAF_CODEX_APP_SERVER_TRACE_EVENTS=1 to print raw Codex App Server JSON-RPC events.");
    Console.WriteLine("Add --trace-copilot-events or set MCAF_GITHUB_COPILOT_TRACE_EVENTS=1 to print GitHub Copilot SDK wrapper diagnostics.");
}

static async Task<int> RunCopilotAsync(GitHubCopilotAgentClient copilot)
{
    if (!IsEnabled("MCAF_GITHUB_COPILOT_INTEGRATION"))
    {
        Console.WriteLine("GitHub Copilot execution skipped. Set MCAF_GITHUB_COPILOT_INTEGRATION=1 to run it.");
        return 0;
    }

    var response = await copilot.SendTurnAsync(CreateCopilotRequest(streaming: false));
    Console.WriteLine(response.Text);
    PrintCopilotResponseInfo(response);
    return 0;
}

static async Task<int> StreamCopilotAsync(GitHubCopilotAgentClient copilot)
{
    if (!IsEnabled("MCAF_GITHUB_COPILOT_INTEGRATION"))
    {
        Console.WriteLine("GitHub Copilot streaming skipped. Set MCAF_GITHUB_COPILOT_INTEGRATION=1 to run it.");
        return 0;
    }

    try
    {
        await foreach (var update in copilot.StreamTurnAsync(CreateCopilotRequest(streaming: true)))
        {
            if (update.Kind == GitHubCopilotStreamingUpdateKind.Delta)
            {
                Console.Write(update.TextDelta);
            }
            else if (update.Kind == GitHubCopilotStreamingUpdateKind.Completed)
            {
                Console.WriteLine();
                PrintCopilotStreamingUpdateInfo(update);
            }
        }

        return 0;
    }
    catch (RuntimeFeatureNotSupportedException ex)
    {
        Trace.TraceError(ex.ToString());
        Console.Error.WriteLine($"GitHub Copilot streaming is not supported by this runtime configuration: {ex.Message}");
        return 1;
    }
}

static async Task<int> RunCodexAsync(CodexAppServerAgentClient codex)
{
    if (!IsEnabled("MCAF_CODEX_APP_SERVER_INTEGRATION"))
    {
        Console.WriteLine("Codex App Server execution skipped. Set MCAF_CODEX_APP_SERVER_INTEGRATION=1 to run it.");
        return 0;
    }

    var response = await codex.ExecuteTurnAsync(CreateCodexRequest());
    Console.WriteLine(response.Text);
    PrintCodexResponseInfo(response);
    return 0;
}

static async Task<int> StreamCodexAsync(CodexAppServerAgentClient codex)
{
    if (!IsEnabled("MCAF_CODEX_APP_SERVER_INTEGRATION"))
    {
        Console.WriteLine("Codex App Server streaming skipped. Set MCAF_CODEX_APP_SERVER_INTEGRATION=1 to run it.");
        return 0;
    }

    var sawDelta = false;
    await foreach (var update in codex.StreamTurnAsync(CreateCodexRequest()))
    {
        if (update.Kind == CodexAppServerStreamingUpdateKind.Delta)
        {
            Console.Write(update.TextDelta);
            sawDelta = true;
        }
        else if (!sawDelta && update.Kind == CodexAppServerStreamingUpdateKind.Completed && !string.IsNullOrEmpty(update.FinalText))
        {
            Console.Write(update.FinalText);
        }

        if (update.Kind is CodexAppServerStreamingUpdateKind.Completed or CodexAppServerStreamingUpdateKind.Error)
        {
            Console.WriteLine();
            PrintCodexStreamingUpdateInfo(update);
        }
    }

    return 0;
}

static void PrintCopilotResponseInfo(GitHubCopilotAgentResponse response)
{
    Console.WriteLine($"Runtime={FormatValue(response.RuntimeName)}; ModelId={FormatValue(response.ModelId)}; RequestId={FormatValue(response.RequestId)}; TraceId={FormatValue(response.TraceId)}; FinishStatus={FormatValue(response.FinishStatus)}; ElapsedTime={response.ElapsedTime}");
    PrintOptionalLine("DiagnosticsSummary", response.DiagnosticsSummary);
    PrintSdkMetadata(response.SdkMetadata);
}

static void PrintCopilotStreamingUpdateInfo(GitHubCopilotStreamingUpdate update)
{
    Console.WriteLine($"Kind={update.Kind}; Runtime={FormatValue(update.RuntimeName)}; RequestId={FormatValue(update.RequestId)}; TraceId={FormatValue(update.TraceId)}; FinishStatus={FormatValue(update.FinishStatus)}; DeltaCount={FormatValue(update.DeltaCount)}; AccumulatedLength={FormatValue(update.AccumulatedLength)}; ElapsedTime={FormatValue(update.ElapsedTime)}");
    PrintOptionalLine("DiagnosticsSummary", update.DiagnosticsSummary);
    PrintSdkMetadata(update.SdkMetadata);
}

static void PrintCodexResponseInfo(CodexAppServerTurnResponse response)
{
    Console.WriteLine($"RequestId={FormatValue(response.RequestId)}; TraceId={FormatValue(response.TraceId)}; ThreadId={FormatValue(response.ThreadId)}; TurnId={FormatValue(response.TurnId)}; Status={FormatValue(response.Status)}; JsonRpcTurnStartRequestId={FormatValue(response.JsonRpcTurnStartRequestId)}");
    PrintOptionalLine("ErrorSummary", response.ErrorSummary);
    PrintOptionalLine("DiagnosticsSummary", response.DiagnosticsSummary);
}

static void PrintCodexStreamingUpdateInfo(CodexAppServerStreamingUpdate update)
{
    Console.WriteLine($"Kind={update.Kind}; RequestId={FormatValue(update.RequestId)}; TraceId={FormatValue(update.TraceId)}; ThreadId={FormatValue(update.ThreadId)}; TurnId={FormatValue(update.TurnId)}; Status={FormatValue(update.Status)}; JsonRpcTurnStartRequestId={FormatValue(update.JsonRpcTurnStartRequestId)}");
    PrintOptionalLine("ErrorSummary", update.ErrorSummary);
    PrintOptionalLine("DiagnosticsSummary", update.DiagnosticsSummary);
}

static void PrintSdkMetadata(IReadOnlyDictionary<string, object?>? sdkMetadata)
{
    if (sdkMetadata is null || sdkMetadata.Count == 0)
    {
        return;
    }

    Console.WriteLine("SdkMetadata:");
    foreach (var entry in sdkMetadata.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($"  {entry.Key}={FormatValue(entry.Value)}");
    }
}

static void PrintOptionalLine(string name, string? value)
{
    if (!string.IsNullOrWhiteSpace(value))
    {
        Console.WriteLine($"{name}={value}");
    }
}

static string FormatValue(object? value) => value switch
{
    null => "(none)",
    string text when string.IsNullOrWhiteSpace(text) => "(none)",
    _ => value.ToString() ?? "(none)",
};

static GitHubCopilotAgentRequest CreateCopilotRequest(bool streaming) => new()
{
    Prompt = "Return one concise sentence explaining what this repository does.",
    ModelId = "gpt-5-mini",
    ReasoningEffort = ReasoningEffortLevel.Medium,
    Streaming = streaming,
    WorkingDirectory = Environment.CurrentDirectory,
    TimeoutSeconds = 30,
    PermissionHandling = GitHubCopilotPermissionHandlingMode.DenyAll,
    AvailableTools = ["read_file"],
    ExcludedTools = ["run_shell_command"],
    SkillDirectories = [Path.Combine(Environment.CurrentDirectory, ".agents", "skills")],
    DisabledSkills = ["legacy-skill"],
};

static CodexAppServerTurnRequest CreateCodexRequest() => new()
{
    Prompt = "Return one concise sentence explaining what this repository does.",
    ModelId = "gpt-5.4",
    ReasoningEffort = CodexReasoningEffort.Medium,
    WorkingDirectory = Environment.CurrentDirectory,
    ApprovalPolicy = "never",
    SandboxMode = "workspace-write",
    NetworkAccess = false,
    AutoApprove = false,
    ThreadReusePolicy = CodexThreadReusePolicy.AlwaysNew,
    TimeoutSeconds = 1800,
    CaptureEventsForDiagnostics = true,
    Summary = "concise",
    Personality = "pragmatic",
};

static bool HasArg(string[] args, string expected)
    => args.Contains(expected, StringComparer.OrdinalIgnoreCase);

static bool IsEnabled(string variableName)
{
    var value = Environment.GetEnvironmentVariable(variableName);
    return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
