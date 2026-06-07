using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MultiCodingAgentFacade.CodexAppServer.Tests.Fakes;
using MultiCodingAgentFacade.CodexAppServer.Threading;
using MultiCodingAgentFacade.Core.Exceptions;
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
            },
            second => Assert.Equal(CodexAppServerStreamingUpdateKind.Completed, second.Kind));
    }

    [Fact]
    public async Task ExecuteTurnAsync_ThrowsRuntimeOperationExceptionWhenTurnFails()
    {
        var transport = CreateFailingTransport();
        var sut = CreateClient(transport);

        var ex = await Assert.ThrowsAsync<RuntimeOperationException>(() =>
            sut.ExecuteTurnAsync(new CodexAppServerTurnRequest { Prompt = "hello" }));

        Assert.Equal("CodexAppServer", ex.RuntimeName);
        Assert.Contains("model overload", ex.Message, StringComparison.Ordinal);
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
