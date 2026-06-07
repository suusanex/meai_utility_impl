using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using MultiCodingAgentFacade.CodexAppServer.Abstractions;

namespace MultiCodingAgentFacade.CodexAppServer.Tests.Fakes;

internal sealed class ScriptedCodexTransport : ICodexTransport
{
    private readonly Channel<string> serverLines = Channel.CreateUnbounded<string>();

    public List<string> SentLines { get; } = [];
    public Func<JsonElement, ScriptedCodexTransport, CancellationToken, Task>? OnClientMessageAsync { get; set; }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        SentLines.Add(line);
        if (OnClientMessageAsync is null)
        {
            return;
        }

        using var document = JsonDocument.Parse(line);
        await OnClientMessageAsync(document.RootElement, this, cancellationToken);
    }

    public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await serverLines.Reader.WaitToReadAsync(cancellationToken))
        {
            while (serverLines.Reader.TryRead(out var line))
            {
                yield return line;
            }
        }
    }

    public Task EnqueueServerMessageAsync(string line, CancellationToken cancellationToken = default)
        => serverLines.Writer.WriteAsync(line, cancellationToken).AsTask();

    public void CompleteServerMessages() => serverLines.Writer.TryComplete();

    public ValueTask DisposeAsync()
    {
        serverLines.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}

internal sealed class StubCodexTransportFactory(ICodexTransport transport) : ICodexTransportFactory
{
    public string? LastWorkingDirectory { get; private set; }

    public ICodexTransport Create(string? workingDirectory)
    {
        LastWorkingDirectory = workingDirectory;
        return transport;
    }
}
