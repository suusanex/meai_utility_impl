using System.Text.Json;
using MeAiUtility.MultiProvider.CodexAppServer.Options;
using MeAiUtility.MultiProvider.CodexAppServer.Tests.Fakes;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.CodexAppServer.Tests;

public class CodexAppServerChatClientTests
{
    [Test]
    public async Task GetResponseAsync_MapsThreadAndTurnPayloads()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
                return;
            }

            if (IsRequest(message, "thread/start"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"thread":{"id":"thread-1"},"approvalPolicy":"never","sandbox":"workspace-write"}"""), cancellationToken);
                return;
            }

            if (IsRequest(message, "turn/start"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"turn":{"id":"turn-1"}}"""), cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"method":"item/agentMessage/delta","params":{"itemId":"item-1","threadId":"thread-1","turnId":"turn-1","delta":"Hello "}}""", cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"method":"item/agentMessage/delta","params":{"itemId":"item-1","threadId":"thread-1","turnId":"turn-1","delta":"World"}}""", cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed","items":[]}}}""", cancellationToken);
                fake.CompleteServerMessages();
            }
        };

        var factory = new StubCodexTransportFactory(transport);
        var providerOptions = new CodexAppServerProviderOptions
        {
            TimeoutSeconds = 30,
            SandboxMode = "workspace-write",
            NetworkAccess = false,
            ApprovalPolicy = "never",
        };

        var sut = new CodexAppServerChatClient(
            providerOptions,
            factory,
            new NullLogger<CodexAppServerChatClient>(),
            NullLoggerFactory.Instance);

        var options = CreateExecutionOptions(new ConversationExecutionOptions
        {
            ModelId = "gpt-5.4",
            ReasoningEffort = ReasoningEffortLevel.High,
            WorkingDirectory = @"D:\work",
        });

        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")], options);

        Assert.That(response.Text, Is.EqualTo("Hello World"));

        using var threadStart = ParseSentMessage(transport, "thread/start");
        var threadParams = threadStart.RootElement.GetProperty("params");
        Assert.That(threadParams.GetProperty("sandbox").GetString(), Is.EqualTo("workspace-write"));
        Assert.That(threadParams.TryGetProperty("networkAccess", out _), Is.False);

        using var turnStart = ParseSentMessage(transport, "turn/start");
        var turnParams = turnStart.RootElement.GetProperty("params");
        Assert.That(turnParams.GetProperty("threadId").GetString(), Is.EqualTo("thread-1"));
        Assert.That(turnParams.GetProperty("input")[0].GetProperty("type").GetString(), Is.EqualTo("text"));
        Assert.That(turnParams.GetProperty("effort").GetString(), Is.EqualTo("high"));
        Assert.That(turnParams.GetProperty("sandboxPolicy").GetProperty("type").GetString(), Is.EqualTo("workspaceWrite"));
        Assert.That(turnParams.GetProperty("sandboxPolicy").GetProperty("networkAccess").GetBoolean(), Is.False);
        Assert.That(turnParams.TryGetProperty("networkAccess", out _), Is.False);
        Assert.That(factory.LastWorkingDirectory, Is.EqualTo(@"D:\work"));
    }

    [Test]
    public void GetResponseAsync_ThrowsProviderException_WhenTurnFailed()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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

        var sut = CreateSut(transport);
        var ex = Assert.ThrowsAsync<ProviderException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]));
        Assert.That(ex!.Message, Does.Contain("model overload"));
    }

    [Test]
    public async Task GetResponseAsync_ContinuesWhenErrorWillRetryIsTrue()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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
                await fake.EnqueueServerMessageAsync("""{"method":"error","params":{"threadId":"thread-1","turnId":"turn-1","willRetry":true,"error":{"message":"retrying"}}}""", cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"method":"item/agentMessage/delta","params":{"itemId":"item-1","threadId":"thread-1","turnId":"turn-1","delta":"done"}}""", cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed","items":[]}}}""", cancellationToken);
                fake.CompleteServerMessages();
            }
        };

        var sut = CreateSut(transport);
        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);
        Assert.That(response.Text, Is.EqualTo("done"));
    }

    [Test]
    public void GetResponseAsync_FailsWhenErrorWillRetryIsFalse()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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
                await fake.EnqueueServerMessageAsync("""{"method":"error","params":{"threadId":"thread-1","turnId":"turn-1","willRetry":false,"error":{"message":"fatal"}}}""", cancellationToken);
                fake.CompleteServerMessages();
            }
        };

        var sut = CreateSut(transport);
        var ex = Assert.ThrowsAsync<ProviderException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]));
        Assert.That(ex!.Message, Does.Contain("fatal"));
    }

    [Test]
    public void GetResponseAsync_ThrowsWhenTurnCompletedMissingTurnId()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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
                await fake.EnqueueServerMessageAsync("""{"method":"turn/completed","params":{"threadId":"thread-1","turn":{"status":"completed","items":[]}}}""", cancellationToken);
            }
        };

        var sut = CreateSut(transport);
        var ex = Assert.ThrowsAsync<ProviderException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]));
        Assert.That(ex!.Message, Does.Contain("id"));
    }

    [Test]
    public void GetResponseAsync_ThrowsOperationCanceledException_WhenTurnInterrupted()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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
                await fake.EnqueueServerMessageAsync("""{"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"interrupted","items":[]}}}""", cancellationToken);
            }
        };

        var sut = CreateSut(transport);
        Assert.ThrowsAsync<OperationCanceledException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]));
    }

    [Test]
    public void GetResponseAsync_ThrowsWhenErrorNotificationMissingTurnId()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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
                await fake.EnqueueServerMessageAsync("""{"method":"error","params":{"threadId":"thread-1","willRetry":false,"error":{"message":"fatal"}}}""", cancellationToken);
            }
        };

        var sut = CreateSut(transport);
        var ex = Assert.ThrowsAsync<ProviderException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]));
        Assert.That(ex!.Message, Does.Contain("turnId"));
    }

    [Test]
    public void GetResponseAsync_ThrowsWhenThreadStatusChangedMissingType()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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
                await fake.EnqueueServerMessageAsync("""{"method":"thread/status/changed","params":{"threadId":"thread-1","status":{"activeFlags":["waitingOnApproval"]}}}""", cancellationToken);
            }
        };

        var sut = CreateSut(transport);
        var ex = Assert.ThrowsAsync<ProviderException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]));
        Assert.That(ex!.Message, Does.Contain("type"));
    }

    [Test]
    public async Task GetResponseAsync_WhenWaitingOnApproval_ContinuesToApprovalRequestFlow()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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
                await fake.EnqueueServerMessageAsync("""{"method":"thread/status/changed","params":{"threadId":"thread-1","status":{"type":"active","activeFlags":["waitingOnApproval"]}}}""", cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"id":"approval-1","method":"item/commandExecution/requestApproval","params":{"threadId":"thread-1","turnId":"turn-1"}}""", cancellationToken);
                return;
            }

            if (IsResponse(message, "approval-1"))
            {
                await fake.EnqueueServerMessageAsync("""{"method":"item/agentMessage/delta","params":{"itemId":"item-1","threadId":"thread-1","turnId":"turn-1","delta":"approved"}}""", cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed","items":[]}}}""", cancellationToken);
                fake.CompleteServerMessages();
            }
        };

        var sut = CreateSut(transport, new CodexAppServerProviderOptions { AutoApprove = true });
        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        Assert.That(response.Text, Is.EqualTo("approved"));
    }

    [Test]
    public void GetResponseAsync_WhenApprovalRequestedAndAutoApproveFalse_ThrowsAndSendsCancel()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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
                await fake.EnqueueServerMessageAsync("""{"id":"approval-1","method":"item/commandExecution/requestApproval","params":{"threadId":"thread-1","turnId":"turn-1"}}""", cancellationToken);
            }
        };

        var providerOptions = new CodexAppServerProviderOptions { AutoApprove = false };
        var sut = new CodexAppServerChatClient(
            providerOptions,
            new StubCodexTransportFactory(transport),
            new NullLogger<CodexAppServerChatClient>(),
            NullLoggerFactory.Instance);

        var ex = Assert.ThrowsAsync<ProviderException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]));
        Assert.That(ex!.Message, Does.Contain("Approval requested"));

        using var approvalResponse = ParseSentResponseById(transport, "approval-1");
        Assert.That(approvalResponse.RootElement.GetProperty("result").GetProperty("decision").GetString(), Is.EqualTo("cancel"));
    }

    [Test]
    public async Task GetResponseAsync_WhenApprovalRequestedAndAutoApproveTrue_Continues()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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
                await fake.EnqueueServerMessageAsync("""{"id":"approval-1","method":"item/commandExecution/requestApproval","params":{"threadId":"thread-1","turnId":"turn-1"}}""", cancellationToken);
                return;
            }

            if (IsResponse(message, "approval-1"))
            {
                await fake.EnqueueServerMessageAsync("""{"method":"item/agentMessage/delta","params":{"itemId":"item-1","threadId":"thread-1","turnId":"turn-1","delta":"approved"}}""", cancellationToken);
                await fake.EnqueueServerMessageAsync("""{"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed","items":[]}}}""", cancellationToken);
                fake.CompleteServerMessages();
            }
        };

        var sut = CreateSut(transport, new CodexAppServerProviderOptions { AutoApprove = true });
        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        Assert.That(response.Text, Is.EqualTo("approved"));

        using var approvalResponse = ParseSentResponseById(transport, "approval-1");
        Assert.That(approvalResponse.RootElement.GetProperty("result").GetProperty("decision").GetString(), Is.EqualTo("acceptForSession"));
    }

    [Test]
    public void GetResponseAsync_ThrowsWhenWaitingOnUserInput()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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

        var sut = CreateSut(transport);
        var ex = Assert.ThrowsAsync<ProviderException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]));
        Assert.That(ex!.Message, Does.Contain("User input required"));
    }

    [Test]
    public void GetResponseAsync_ThrowsTimeoutException_WhenTurnDoesNotComplete()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
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
            }
        };

        var sut = CreateSut(transport, new CodexAppServerProviderOptions { TimeoutSeconds = 1 });
        Assert.That(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]), Throws.InstanceOf<MeAiUtility.MultiProvider.Exceptions.TimeoutException>());
    }

    [Test]
    public void GetResponseAsync_ThrowsInvalidRequestException_WhenTimeoutExtensionExceedsInt32Range()
    {
        var transport = new ScriptedCodexTransport();
        var sut = CreateSut(transport);
        var options = CreateOptionsWithExtensions(("codex.timeoutSeconds", (long)int.MaxValue + 1));

        var ex = Assert.ThrowsAsync<InvalidRequestException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")], options));
        Assert.That(ex!.Message, Does.Contain("codex.timeoutSeconds"));
    }

    private static CodexAppServerChatClient CreateSut(ScriptedCodexTransport transport, CodexAppServerProviderOptions? options = null)
    {
        return new CodexAppServerChatClient(
            options ?? new CodexAppServerProviderOptions(),
            new StubCodexTransportFactory(transport),
            new NullLogger<CodexAppServerChatClient>(),
            NullLoggerFactory.Instance);
    }

    private static ChatOptions CreateExecutionOptions(ConversationExecutionOptions execution)
    {
        var options = new ChatOptions();
        (options.AdditionalProperties ??= new AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = execution;
        return options;
    }

    private static ChatOptions CreateOptionsWithExtensions(params (string Key, object? Value)[] values)
    {
        var ext = new ExtensionParameters();
        foreach (var (key, value) in values)
        {
            ext.Set(key, value);
        }

        var options = new ChatOptions();
        (options.AdditionalProperties ??= new AdditionalPropertiesDictionary())["meai.extensions"] = ext;
        return options;
    }

    private static bool IsRequest(JsonElement message, string methodName)
    {
        return message.TryGetProperty("method", out var method)
            && string.Equals(method.GetString(), methodName, StringComparison.Ordinal)
            && message.TryGetProperty("id", out _);
    }

    private static bool IsResponse(JsonElement message, string id)
    {
        if (!message.TryGetProperty("id", out var idElement) || message.TryGetProperty("method", out _))
        {
            return false;
        }

        return idElement.ValueKind == JsonValueKind.String && string.Equals(idElement.GetString(), id, StringComparison.Ordinal);
    }

    private static string GetId(JsonElement message)
    {
        var idElement = message.GetProperty("id");
        return idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()!
            : idElement.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string CreateResponse(string id, string rawResultJson)
        => $$"""{"id":{{id}},"result":{{rawResultJson}}}""";

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

    private static JsonDocument ParseSentResponseById(ScriptedCodexTransport transport, string id)
    {
        var line = transport.SentLines.First(line =>
        {
            using var document = JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("id", out var idElement)
                || document.RootElement.TryGetProperty("method", out _))
            {
                return false;
            }

            return idElement.ValueKind == JsonValueKind.String
                && string.Equals(idElement.GetString(), id, StringComparison.Ordinal);
        });

        return JsonDocument.Parse(line);
    }
}
