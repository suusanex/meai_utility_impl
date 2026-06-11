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
        services.AddLogging();
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
    Console.WriteLine($"Runtime={response.RuntimeName}; RequestId={response.RequestId}; TraceId={response.TraceId}; FinishStatus={response.FinishStatus ?? "(none)"}");
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
                Console.WriteLine($"Runtime={update.RuntimeName}; RequestId={update.RequestId}; TraceId={update.TraceId}; FinishStatus={update.FinishStatus ?? "(none)"}");
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
    Console.WriteLine($"RequestId={response.RequestId}; TraceId={response.TraceId}; ThreadId={response.ThreadId ?? "(none)"}; TurnId={response.TurnId ?? "(none)"}; Status={response.Status ?? "(none)"}");
    return 0;
}

static async Task<int> StreamCodexAsync(CodexAppServerAgentClient codex)
{
    if (!IsEnabled("MCAF_CODEX_APP_SERVER_INTEGRATION"))
    {
        Console.WriteLine("Codex App Server streaming skipped. Set MCAF_CODEX_APP_SERVER_INTEGRATION=1 to run it.");
        return 0;
    }

    await foreach (var update in codex.StreamTurnAsync(CreateCodexRequest()))
    {
        if (update.Kind == CodexAppServerStreamingUpdateKind.Delta)
        {
            Console.Write(update.TextDelta);
        }
        else if (update.Kind is CodexAppServerStreamingUpdateKind.Completed or CodexAppServerStreamingUpdateKind.Error)
        {
            Console.WriteLine();
            Console.WriteLine($"RequestId={update.RequestId}; TraceId={update.TraceId}; ThreadId={update.ThreadId ?? "(none)"}; TurnId={update.TurnId ?? "(none)"}; Status={update.Status ?? "(none)"}");
        }
    }

    return 0;
}

static GitHubCopilotAgentRequest CreateCopilotRequest(bool streaming) => new()
{
    Prompt = "Return one concise sentence explaining what this repository does.",
    ModelId = "gpt-5",
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
    ModelId = "gpt-5",
    ReasoningEffort = CodexReasoningEffort.Medium,
    WorkingDirectory = Environment.CurrentDirectory,
    ApprovalPolicy = "never",
    SandboxMode = "workspace-write",
    NetworkAccess = false,
    AutoApprove = false,
    ThreadReusePolicy = CodexThreadReusePolicy.AlwaysNew,
    TimeoutSeconds = 30,
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
