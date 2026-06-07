using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MultiCodingAgentFacade.CodexAppServer.Tests.Fakes;
using MultiCodingAgentFacade.CodexAppServer.Threading;
using Xunit;

namespace MultiCodingAgentFacade.CodexAppServer.Tests;

public sealed class CodexAppServerAgentClientTests
{
    [Fact]
    public async Task ExecuteTurnAsync_MapsTurnRequestToJsonRpcPayloads()
    {
        var transport = CreateCompletingTransport("Hello World");
        var factory = new StubCodexTransportFactory(transport);
        var sut = CreateClient(transport, factory);

        var response = await sut.ExecuteTurnAsync(new CodexAppServerTurnRequest
        {
            Prompt = "hello",
            ModelId = "gpt-5.4",
            ReasoningEffort = CodexReasoningEffort.High,
            WorkingDirectory = @"D:\work",
            SandboxMode = "workspace-write",
            NetworkAccess = false,
        });

        Assert.Equal("Hello World", response.Text);
        Assert.Equal("thread-1", response.ThreadId);
        Assert.Equal("turn-1", response.TurnId);
        Assert.Equal("completed", response.Status);
        Assert.False(string.IsNullOrWhiteSpace(response.TraceId));
        Assert.False(string.IsNullOrWhiteSpace(response.RequestId));
        Assert.Equal("3", response.JsonRpcTurnStartRequestId);
        Assert.Null(response.ErrorSummary);
        Assert.Equal(@"D:\work", factory.LastWorkingDirectory);

        using var threadStart = ParseSentMessage(transport, "thread/start");
        var threadParams = threadStart.RootElement.GetProperty("params");
        Assert.Equal("gpt-5.4", threadParams.GetProperty("model").GetString());
        Assert.Equal("workspace-write", threadParams.GetProperty("sandbox").GetString());

        using var turnStart = ParseSentMessage(transport, "turn/start");
        var turnParams = turnStart.RootElement.GetProperty("params");
        Assert.Equal("thread-1", turnParams.GetProperty("threadId").GetString());
        Assert.Equal("hello", turnParams.GetProperty("input")[0].GetProperty("text").GetString());
        Assert.Equal("high", turnParams.GetProperty("effort").GetString());
        Assert.Equal("workspaceWrite", turnParams.GetProperty("sandboxPolicy").GetProperty("type").GetString());
        Assert.False(turnParams.GetProperty("sandboxPolicy").GetProperty("networkAccess").GetBoolean());
    }

    [Fact]
    public async Task StreamTurnAsync_ReturnsDeltaUpdatesAndCompletedUpdate()
    {
        var transport = CreateCompletingTransport("Hello World");
        var sut = CreateClient(transport);

        var updates = new List<CodexAppServerStreamingUpdate>();
        await foreach (var update in sut.StreamTurnAsync(new CodexAppServerTurnRequest { Prompt = "hello" }))
        {
            updates.Add(update);
        }

        Assert.Collection(
            updates,
            first =>
            {
                Assert.Equal(CodexAppServerStreamingUpdateKind.Delta, first.Kind);
                Assert.Equal("Hello World", first.TextDelta);
                Assert.Equal("thread-1", first.ThreadId);
                Assert.Equal("turn-1", first.TurnId);
                Assert.False(string.IsNullOrWhiteSpace(first.TraceId));
                Assert.False(string.IsNullOrWhiteSpace(first.RequestId));
            },
            second =>
            {
                Assert.Equal(CodexAppServerStreamingUpdateKind.Completed, second.Kind);
                Assert.Equal("Hello World", second.FinalText);
                Assert.Equal("thread-1", second.ThreadId);
                Assert.Equal("turn-1", second.TurnId);
                Assert.Equal("completed", second.Status);
                Assert.False(string.IsNullOrWhiteSpace(second.TraceId));
                Assert.False(string.IsNullOrWhiteSpace(second.RequestId));
            });
    }

    [Fact]
    public async Task ExecuteTurnAsync_ReturnsFailedTurnMetadataWhenTurnFails()
    {
        var transport = CreateFailingTransport();
        var sut = CreateClient(transport);

        var response = await sut.ExecuteTurnAsync(new CodexAppServerTurnRequest { Prompt = "hello" });

        Assert.Equal(string.Empty, response.Text);
        Assert.Equal("thread-1", response.ThreadId);
        Assert.Equal("turn-1", response.TurnId);
        Assert.Equal("failed", response.Status);
        Assert.Equal("model overload", response.ErrorSummary);
        Assert.False(string.IsNullOrWhiteSpace(response.TraceId));
        Assert.False(string.IsNullOrWhiteSpace(response.RequestId));
    }

    [Fact]
    public async Task StreamTurnAsync_ReturnsErrorUpdateWhenTurnFails()
    {
        var transport = CreateFailingTransport();
        var sut = CreateClient(transport);

        var updates = new List<CodexAppServerStreamingUpdate>();
        await foreach (var update in sut.StreamTurnAsync(new CodexAppServerTurnRequest { Prompt = "hello" }))
        {
            updates.Add(update);
        }

        var errorUpdate = Assert.Single(updates);
        Assert.Equal(CodexAppServerStreamingUpdateKind.Error, errorUpdate.Kind);
        Assert.Equal("failed", errorUpdate.Status);
        Assert.Equal("model overload", errorUpdate.ErrorSummary);
        Assert.Equal("thread-1", errorUpdate.ThreadId);
        Assert.Equal("turn-1", errorUpdate.TurnId);
        Assert.False(string.IsNullOrWhiteSpace(errorUpdate.TraceId));
        Assert.False(string.IsNullOrWhiteSpace(errorUpdate.RequestId));
    }

    [Fact]
    public async Task ExecuteTurnAsync_ReturnsErrorNotificationMetadataWhenNonRetryErrorArrives()
    {
        var transport = CreateErrorNotificationTransport();
        var sut = CreateClient(transport);

        var response = await sut.ExecuteTurnAsync(new CodexAppServerTurnRequest { Prompt = "hello" });

        Assert.Equal("error", response.Status);
        Assert.Equal("request rejected", response.ErrorSummary);
        Assert.Equal("thread-1", response.ThreadId);
        Assert.Equal("turn-1", response.TurnId);
    }

    [Fact]
    public async Task ExecuteTurnAsync_ReturnsWaitingOnUserInputStatus()
    {
        var transport = CreateWaitingOnUserInputTransport();
        var sut = CreateClient(transport);

        var response = await sut.ExecuteTurnAsync(new CodexAppServerTurnRequest { Prompt = "hello" });

        Assert.Equal("waitingOnUserInput", response.Status);
        Assert.Equal("User input required by codex app-server.", response.ErrorSummary);
        Assert.Equal("thread-1", response.ThreadId);
        Assert.Equal("turn-1", response.TurnId);
    }

    private static CodexAppServerAgentClient CreateClient(
        ScriptedCodexTransport transport,
        StubCodexTransportFactory? factory = null)
    {
        return new CodexAppServerAgentClient(
            new Options.CodexAppServerOptions { TimeoutSeconds = 30 },
            factory ?? new StubCodexTransportFactory(transport),
            new StubCodexThreadStore(),
            new NullLogger<CodexAppServerAgentClient>(),
            NullLoggerFactory.Instance);
    }

    private static ScriptedCodexTransport CreateCompletingTransport(string text)
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test"}"""), cancellationToken);
                return;
            }

            if (IsRequest(message, "thread/start"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"thread":{"id":"thread-1"}}"""), cancellationToken);
                return;
            }

            if (IsRequest(message, "turn/start"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"turn":{"id":"turn-1"}}"""), cancellationToken);
                var deltaJson = JsonSerializer.Serialize(text);
                await fake.EnqueueServerMessageAsync(
                    "{\"method\":\"item/agentMessage/delta\",\"params\":{\"itemId\":\"item-1\",\"threadId\":\"thread-1\",\"turnId\":\"turn-1\",\"delta\":" + deltaJson + "}}",
                    cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed","items":[]}}}""", cancellationToken);
                fake.CompleteServerMessages();
            }
        };

        return transport;
    }

    private static ScriptedCodexTransport CreateFailingTransport()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test"}"""), cancellationToken);
                return;
            }

            if (IsRequest(message, "thread/start"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"thread":{"id":"thread-1"}}"""), cancellationToken);
                return;
            }

            if (IsRequest(message, "turn/start"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"turn":{"id":"turn-1"}}"""), cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"failed","error":{"message":"model overload"},"items":[]}}}""", cancellationToken);
                fake.CompleteServerMessages();
            }
        };

        return transport;
    }

    private static ScriptedCodexTransport CreateErrorNotificationTransport()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test"}"""), cancellationToken);
                return;
            }

            if (IsRequest(message, "thread/start"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"thread":{"id":"thread-1"}}"""), cancellationToken);
                return;
            }

            if (IsRequest(message, "turn/start"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"turn":{"id":"turn-1"}}"""), cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"method":"error","params":{"threadId":"thread-1","turnId":"turn-1","willRetry":false,"error":{"message":"request rejected"}}}""", cancellationToken);
                fake.CompleteServerMessages();
            }
        };

        return transport;
    }

    private static ScriptedCodexTransport CreateWaitingOnUserInputTransport()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test"}"""), cancellationToken);
                return;
            }

            if (IsRequest(message, "thread/start"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"thread":{"id":"thread-1"}}"""), cancellationToken);
                return;
            }

            if (IsRequest(message, "turn/start"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"turn":{"id":"turn-1"}}"""), cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"method":"thread/status/changed","params":{"threadId":"thread-1","status":{"type":"active","activeFlags":["waitingOnUserInput"]}}}""", cancellationToken);
                fake.CompleteServerMessages();
            }
        };

        return transport;
    }

    private static bool IsRequest(JsonElement message, string methodName)
    {
        return message.TryGetProperty("method", out var method)
            && string.Equals(method.GetString(), methodName, StringComparison.Ordinal)
            && message.TryGetProperty("id", out _);
    }

    private static string GetId(JsonElement message)
    {
        var idElement = message.GetProperty("id");
        return idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()!
            : idElement.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string CreateResponse(string id, string rawResultJson)
        => "{\"id\":" + id + ",\"result\":" + rawResultJson + "}";

    private static JsonDocument ParseSentMessage(ScriptedCodexTransport transport, string methodName)
    {
        var line = transport.SentLines.First(line =>
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.TryGetProperty("method", out var method)
                && string.Equals(method.GetString(), methodName, StringComparison.Ordinal);
        });

        return JsonDocument.Parse(line);
    }
}
