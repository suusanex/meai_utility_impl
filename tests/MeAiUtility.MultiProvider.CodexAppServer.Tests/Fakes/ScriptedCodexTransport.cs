using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;

namespace MeAiUtility.MultiProvider.CodexAppServer.Tests.Fakes;

internal sealed class ScriptedCodexTransport : ICodexTransport
{
    private readonly Channel<string> _serverLines = Channel.CreateUnbounded<string>();

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
        while (await _serverLines.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_serverLines.Reader.TryRead(out var line))
            {
                yield return line;
            }
        }
    }

    public Task EnqueueServerMessageAsync(string line, CancellationToken cancellationToken = default)
        => _serverLines.Writer.WriteAsync(line, cancellationToken).AsTask();

    public void CompleteServerMessages() => _serverLines.Writer.TryComplete();

    public ValueTask DisposeAsync()
    {
        _serverLines.Writer.TryComplete();
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
