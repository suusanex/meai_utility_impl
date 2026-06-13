extern alias GitHubCopilotSdk;

using MultiCodingAgentFacade.Core.Exceptions;
using MultiCodingAgentFacade.GitHubCopilot.Abstractions;
using MultiCodingAgentFacade.GitHubCopilot.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CopilotSdk = GitHubCopilotSdk::GitHub.Copilot;
using System.Text.Json;

namespace MultiCodingAgentFacade.GitHubCopilot.Tests;

public sealed class GitHubCopilotSdkWrapperTests
{
    [Fact]
    public void BuildRuntimeConnection_MapsUriConnection()
    {
        var connection = GitHubCopilotSdkWrapper.BuildRuntimeConnection(new GitHubCopilotOptions
        {
            CliUrl = "http://localhost:4141",
            ConnectionToken = "token-1",
        });

        var typed = Assert.IsType<CopilotSdk.UriRuntimeConnection>(connection);
        Assert.Equal("http://localhost:4141", typed.Url);
        Assert.Equal("token-1", typed.ConnectionToken);
    }

    [Fact]
    public void BuildRuntimeConnection_MapsStdioAndTcpConnections()
    {
        var stdio = GitHubCopilotSdkWrapper.BuildRuntimeConnection(new GitHubCopilotOptions
        {
            CliPath = "copilot",
            CliArgs = ["server"],
            UseStdio = true,
        });
        var tcp = GitHubCopilotSdkWrapper.BuildRuntimeConnection(new GitHubCopilotOptions
        {
            CliPath = "copilot",
            CliArgs = ["server"],
            UseStdio = false,
            ConnectionToken = "token-2",
        });

        var stdioTyped = Assert.IsType<CopilotSdk.StdioRuntimeConnection>(stdio);
        Assert.Equal("copilot", stdioTyped.Path);
        Assert.Equal(["server"], stdioTyped.Args);

        var tcpTyped = Assert.IsType<CopilotSdk.TcpRuntimeConnection>(tcp);
        Assert.Equal("copilot", tcpTyped.Path);
        Assert.Equal(["server"], tcpTyped.Args);
        Assert.Equal("token-2", tcpTyped.ConnectionToken);
        Assert.Equal(0, tcpTyped.Port);
    }

    [Fact]
    public async Task ListModelsAsync_AutoStartFalseFailsFastBeforeCore()
    {
        var coreCalled = false;
        var wrapper = new GitHubCopilotSdkWrapper(
            new GitHubCopilotOptions { AutoStart = false },
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            _ =>
            {
                coreCalled = true;
                return Task.FromResult<IReadOnlyList<CopilotModelInfo>>([]);
            },
            null);

        var ex = await Assert.ThrowsAsync<RuntimeInvalidRequestException>(() => wrapper.ListModelsAsync());

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
        Assert.Contains("AutoStart", ex.Message);
        Assert.False(coreCalled);
    }

    [Fact]
    public async Task ListModelsAsync_AutoRestartFalseFailsFastBeforeCore()
    {
        var coreCalled = false;
        var wrapper = new GitHubCopilotSdkWrapper(
            new GitHubCopilotOptions { AutoRestart = false },
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            _ =>
            {
                coreCalled = true;
                return Task.FromResult<IReadOnlyList<CopilotModelInfo>>([]);
            },
            null);

        var ex = await Assert.ThrowsAsync<RuntimeInvalidRequestException>(() => wrapper.ListModelsAsync());

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
        Assert.Contains("AutoRestart", ex.Message);
        Assert.False(coreCalled);
    }

    [Fact]
    public void DescribeRuntimeConnection_SanitizesUriAndMasksRuntimePath()
    {
        var uriDescription = GitHubCopilotSdkWrapper.DescribeRuntimeConnection(new GitHubCopilotOptions
        {
            CliUrl = "https://user:secret@example.test:8443/runtime?token=secret#fragment",
        });
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var runtimePath = Path.Combine(userProfile, "secret", "copilot.exe");
        var pathDescription = GitHubCopilotSdkWrapper.DescribeRuntimeConnection(new GitHubCopilotOptions
        {
            CliPath = runtimePath,
            UseStdio = true,
        });

        Assert.Equal("uri:https://example.test:8443", uriDescription);
        Assert.DoesNotContain("user", uriDescription);
        Assert.DoesNotContain("secret", uriDescription);
        Assert.DoesNotContain("token", uriDescription);
        Assert.DoesNotContain(userProfile, pathDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("none", "none")]
    [InlineData("ERROR", "error")]
    [InlineData("Warning", "warning")]
    [InlineData("Info", "info")]
    [InlineData("debug", "debug")]
    [InlineData("ALL", "all")]
    public void MapLogLevel_MapsKnownValuesCaseInsensitively(string input, string expected)
    {
        var logLevel = GitHubCopilotSdkWrapper.MapLogLevel(input);

        Assert.Equal(expected, logLevel?.Value);
    }

    [Fact]
    public void MapLogLevel_InvalidValueFailsFast()
    {
        var ex = Assert.Throws<RuntimeInvalidRequestException>(() => GitHubCopilotSdkWrapper.MapLogLevel("verbose"));

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
        Assert.Contains("LogLevel", ex.Message);
    }

    [Fact]
    public async Task BuildPermissionHandler_MapsDenyAllAndNoResult()
    {
        var denyAll = GitHubCopilotSdkWrapper.BuildPermissionHandler(GitHubCopilotPermissionHandlingMode.DenyAll);
        var noResult = GitHubCopilotSdkWrapper.BuildPermissionHandler(GitHubCopilotPermissionHandlingMode.NoResult);

        var denyDecision = await denyAll(null!, null!);
        var noResultDecision = await noResult(null!, null!);

        Assert.Equal("reject", denyDecision.Kind);
        Assert.Equal("no-result", noResultDecision.Kind);
    }

    [Fact]
    public void BuildInvocation_AutoStartFalseFailsFast()
    {
        var ex = Assert.Throws<RuntimeInvalidRequestException>(() =>
            GitHubCopilotSdkWrapper.BuildInvocation("hello", new CopilotSessionConfig(), new GitHubCopilotOptions { AutoStart = false }));

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
        Assert.Contains("AutoStart", ex.Message);
    }

    [Fact]
    public void BuildInvocation_RejectsPerRequestBaseDirectoryAdvancedOption()
    {
        var config = new CopilotSessionConfig();
        config.AdvancedOptions["copilot.baseDirectory"] = "/tmp/restricted";

        var ex = Assert.Throws<RuntimeInvalidRequestException>(() =>
            GitHubCopilotSdkWrapper.BuildInvocation("hello", config, new GitHubCopilotOptions()));

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
        Assert.Contains("copilot.baseDirectory", ex.Message);
    }

    [Fact]
    public void BuildSdkSessionConfig_DisablesSubAgentStreamingEvents()
    {
        var invocation = GitHubCopilotSdkWrapper.BuildInvocation(
            "hello",
            new CopilotSessionConfig { Streaming = true },
            new GitHubCopilotOptions());

        var sessionConfig = GitHubCopilotSdkWrapper.BuildSdkSessionConfig(invocation);

        Assert.False(sessionConfig.IncludeSubAgentStreamingEvents);
    }

    [Fact]
    public void ValidateMcpServers_MapsStdioAndHttp()
    {
        var mapped = GitHubCopilotSdkWrapper.ValidateMcpServers(new Dictionary<string, object>
        {
            ["stdio-server"] = new Dictionary<string, object>
            {
                ["type"] = "stdio",
                ["command"] = "node",
                ["args"] = new[] { "server.js" },
                ["env"] = new Dictionary<string, string> { ["NODE_ENV"] = "test" },
                ["cwd"] = "D:\\mcp",
                ["tools"] = new[] { "fetch" },
                ["timeout"] = 1500,
            },
            ["http-server"] = new Dictionary<string, object>
            {
                ["type"] = "http",
                ["url"] = "https://example.test/mcp",
                ["headers"] = new Dictionary<string, string> { ["Authorization"] = "Bearer token" },
                ["oauthGrantType"] = "client_credentials",
                ["oauthPublicClient"] = true,
            },
        });

        Assert.NotNull(mapped);

        var stdio = Assert.IsType<CopilotSdk.McpStdioServerConfig>(mapped["stdio-server"]);
        Assert.Equal("node", stdio.Command);
        Assert.Equal(["server.js"], stdio.Args);
        Assert.Equal("test", stdio.Env!["NODE_ENV"]);
        Assert.Equal("D:\\mcp", stdio.WorkingDirectory);
        Assert.Equal(["fetch"], stdio.Tools);
        Assert.Equal(1500, stdio.Timeout);

        var http = Assert.IsType<CopilotSdk.McpHttpServerConfig>(mapped["http-server"]);
        Assert.Equal("https://example.test/mcp", http.Url);
        Assert.Equal("Bearer token", http.Headers!["Authorization"]);
        Assert.Equal(CopilotSdk.McpHttpServerConfigOauthGrantType.ClientCredentials, http.OauthGrantType);
        Assert.True(http.OauthPublicClient);
    }

    [Fact]
    public void ValidateMcpServers_HandlesJsonElementValues()
    {
        using var document = JsonDocument.Parse("""
            {
              "stdio-server": {
                "type": "stdio",
                "command": "node",
                "args": ["server.js"],
                "env": { "NODE_ENV": "test" },
                "cwd": "D:\\mcp",
                "tools": ["fetch"],
                "timeout": 1500
              },
              "http-server": {
                "type": "http",
                "url": "https://example.test/mcp",
                "headers": { "Authorization": "Bearer token" },
                "oauthGrantType": "client_credentials",
                "oauthPublicClient": true
              }
            }
            """);
        var mcpServers = document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object)property.Value.Clone());

        var mapped = GitHubCopilotSdkWrapper.ValidateMcpServers(mcpServers);

        Assert.NotNull(mapped);

        var stdio = Assert.IsType<CopilotSdk.McpStdioServerConfig>(mapped["stdio-server"]);
        Assert.Equal("node", stdio.Command);
        Assert.Equal(["server.js"], stdio.Args);
        Assert.Equal("test", stdio.Env!["NODE_ENV"]);
        Assert.Equal("D:\\mcp", stdio.WorkingDirectory);
        Assert.Equal(["fetch"], stdio.Tools);
        Assert.Equal(1500, stdio.Timeout);

        var http = Assert.IsType<CopilotSdk.McpHttpServerConfig>(mapped["http-server"]);
        Assert.Equal("https://example.test/mcp", http.Url);
        Assert.Equal("Bearer token", http.Headers!["Authorization"]);
        Assert.Equal(CopilotSdk.McpHttpServerConfigOauthGrantType.ClientCredentials, http.OauthGrantType);
        Assert.True(http.OauthPublicClient);
    }

    [Fact]
    public void ValidateMcpServers_RejectsUnknownType()
    {
        var ex = Assert.Throws<RuntimeInvalidRequestException>(() =>
            GitHubCopilotSdkWrapper.ValidateMcpServers(new Dictionary<string, object>
            {
                ["broken"] = new Dictionary<string, object> { ["type"] = "socket" },
            }));

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
        Assert.Contains("unsupported type", ex.Message);
    }

    [Fact]
    public void CreateStreamingUpdate_UsesTypedAssistantEvents()
    {
        var state = new StreamingState();

        var delta = GitHubCopilotSdkWrapper.CreateStreamingUpdate(
            new CopilotSdk.AssistantMessageDeltaEvent
            {
                Data = new CopilotSdk.AssistantMessageDeltaData
                {
                    DeltaContent = "Hel",
                    MessageId = "msg-1",
                },
            },
            state);
        var reasoning = GitHubCopilotSdkWrapper.CreateStreamingUpdate(
            new CopilotSdk.AssistantReasoningDeltaEvent
            {
                Data = new CopilotSdk.AssistantReasoningDeltaData
                {
                    DeltaContent = "internal",
                    ReasoningId = "reason-1",
                },
            },
            state);
        var finalMessage = GitHubCopilotSdkWrapper.CreateStreamingUpdate(
            new CopilotSdk.AssistantMessageEvent
            {
                Data = new CopilotSdk.AssistantMessageData
                {
                    Content = "Hello world",
                    MessageId = "msg-1",
                    RequestId = "req-1",
                    ServiceRequestId = "svc-1",
                },
            },
            state);

        Assert.NotNull(delta);
        Assert.Equal(CopilotStreamingUpdateKind.Delta, delta!.Kind);
        Assert.Equal("Hel", delta.TextDelta);
        Assert.Equal(1, state.DeltaCount);
        Assert.Equal(3, state.AccumulatedLength);

        Assert.NotNull(reasoning);
        Assert.Equal(CopilotStreamingUpdateKind.Progress, reasoning!.Kind);
        Assert.Equal(1, state.ReasoningDeltaCount);
        Assert.Null(reasoning.TextDelta);

        Assert.NotNull(finalMessage);
        Assert.Equal(CopilotStreamingUpdateKind.Progress, finalMessage!.Kind);
        Assert.Equal("Hello world", state.FinalText);
        Assert.Equal("req-1", finalMessage.SdkMetadata!["sdk.requestId"]);
        Assert.Equal("svc-1", finalMessage.SdkMetadata!["sdk.serviceRequestId"]);
    }

    [Fact]
    public void CreateStreamingUpdate_IgnoresSubAgentMessageDelta()
    {
        var state = new StreamingState();

        var update = GitHubCopilotSdkWrapper.CreateStreamingUpdate(
            new CopilotSdk.AssistantMessageDeltaEvent
            {
                AgentId = "sub-agent-1",
                Data = new CopilotSdk.AssistantMessageDeltaData
                {
                    DeltaContent = "assistant.message_delta",
                    MessageId = "msg-1",
                },
            },
            state);

        Assert.NotNull(update);
        Assert.Equal(CopilotStreamingUpdateKind.Progress, update!.Kind);
        Assert.Null(update.TextDelta);
        Assert.Equal(0, state.DeltaCount);
        Assert.Equal(0, state.AccumulatedLength);
        Assert.Equal("sub-agent", update.SdkMetadata!["sdk.ignoredDeltaReason"]);
    }

    [Theory]
    [InlineData("assistant.streaming_delta")]
    [InlineData("assistant.reasoning_delta")]
    [InlineData("assistant.message_delta")]
    [InlineData("assistant.message_start")]
    [InlineData("assistant.message")]
    [InlineData("assistant.turn_start")]
    [InlineData("assistant.turn_end")]
    public void CreateStreamingUpdate_IgnoresEventTypeNameMessageDelta(string eventTypeName)
    {
        var state = new StreamingState();

        var update = GitHubCopilotSdkWrapper.CreateStreamingUpdate(
            new CopilotSdk.AssistantMessageDeltaEvent
            {
                Data = new CopilotSdk.AssistantMessageDeltaData
                {
                    DeltaContent = eventTypeName,
                    MessageId = "msg-1",
                },
            },
            state);

        Assert.NotNull(update);
        Assert.Equal(CopilotStreamingUpdateKind.Progress, update!.Kind);
        Assert.Null(update.TextDelta);
        Assert.Equal(0, state.DeltaCount);
        Assert.Equal(0, state.AccumulatedLength);
        Assert.Equal("event-type", update.SdkMetadata!["sdk.ignoredDeltaReason"]);
    }

    [Fact]
    public void CreateStreamingUpdate_SessionErrorBecomesDiagnosticsOnly()
    {
        var state = new StreamingState();

        var update = GitHubCopilotSdkWrapper.CreateStreamingUpdate(
            new CopilotSdk.SessionErrorEvent
            {
                Data = new CopilotSdk.SessionErrorData
                {
                    ErrorType = "runtime_error",
                    Message = "tool failed",
                    ErrorCode = "E_TOOL",
                    StatusCode = 502,
                    ServiceRequestId = "svc-2",
                },
            },
            state);

        Assert.NotNull(update);
        Assert.Equal(CopilotStreamingUpdateKind.Progress, update!.Kind);
        Assert.Equal("tool failed", update.DiagnosticsSummary);
        Assert.Equal("tool failed", state.LastErrorMessage);
        Assert.Equal("E_TOOL", update.SdkMetadata!["sdk.errorCode"]);
        Assert.Equal(502, update.SdkMetadata!["sdk.statusCode"]);
        Assert.Equal("svc-2", update.SdkMetadata!["sdk.serviceRequestId"]);
    }

    [Fact]
    public void BuildStreamingDiagnosticsSummary_ReflectsReasoningAndSessionError()
    {
        var state = new StreamingState
        {
            ReasoningDeltaCount = 2,
            LastErrorMessage = "transient",
            LastStreamingResponseSizeBytes = 128,
        };

        var summary = GitHubCopilotSdkWrapper.BuildStreamingDiagnosticsSummary(11, state);
        var metadata = GitHubCopilotSdkWrapper.BuildStreamingMetadata(11, state);

        Assert.Contains("11 characters", summary);
        Assert.Contains("Reasoning delta events=2", summary);
        Assert.Contains("transient", summary);
        Assert.Equal(11, metadata["sdk.response.contentLength"]);
        Assert.Equal(2, metadata["sdk.reasoningDeltaCount"]);
        Assert.Equal(128L, metadata["sdk.totalResponseSizeBytes"]);
        Assert.Equal("transient", metadata["sdk.lastSessionError"]);
    }

    [Fact]
    public void BuildStreamingDiagnosticsSummary_TruncatesLongSessionError()
    {
        var longError = new string('x', 400);
        var state = new StreamingState { LastErrorMessage = longError };

        var summary = GitHubCopilotSdkWrapper.BuildStreamingDiagnosticsSummary(11, state);
        var metadata = GitHubCopilotSdkWrapper.BuildStreamingMetadata(11, state);

        var error = Assert.IsType<string>(metadata["sdk.lastSessionError"]);
        Assert.Equal(303, error.Length);
        Assert.EndsWith("...", error);
        Assert.DoesNotContain(longError, summary);
        Assert.Contains(error, summary);
    }

    [Fact]
    public void BuildCliDiagnosticsSummary_MasksSensitiveLocalPaths()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var runtimePath = Path.Combine(userProfile, "secret", "copilot.exe");
        var baseDirectory = Path.Combine(userProfile, ".copilot");
        var wrapper = new GitHubCopilotSdkWrapper(
            new GitHubCopilotOptions
            {
                CliPath = runtimePath,
                BaseDirectory = baseDirectory,
                UseStdio = true,
            },
            NullLogger<GitHubCopilotSdkWrapper>.Instance);

        var summary = wrapper.BuildCliDiagnosticsSummary();

        Assert.DoesNotContain(userProfile, summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CliPath=<", summary);
        Assert.Contains("BaseDirectory=<", summary);
    }

    [Fact]
    public void CreateStreamingUpdate_SessionErrorWhitespaceMessageIsIgnored()
    {
        var state = new StreamingState();

        var update = GitHubCopilotSdkWrapper.CreateStreamingUpdate(
            new CopilotSdk.SessionErrorEvent
            {
                Data = new CopilotSdk.SessionErrorData
                {
                    ErrorType = "runtime_error",
                    Message = "   ",
                    ErrorCode = "E_TOOL",
                },
            },
            state);

        Assert.NotNull(update);
        Assert.Equal(CopilotStreamingUpdateKind.Progress, update!.Kind);
        Assert.Null(state.LastErrorMessage);
    }
}
