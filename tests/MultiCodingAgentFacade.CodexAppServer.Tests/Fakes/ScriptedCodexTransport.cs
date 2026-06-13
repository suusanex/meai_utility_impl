using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using MultiCodingAgentFacade.CodexAppServer.Abstractions;

namespace MultiCodingAgentFacade.CodexAppServer.Tests.Fakes;

internal sealed class ScriptedCodexTransport : ICodexTransport
{
    private readonly TaskCompletionSource<bool> _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<string> serverLines = Channel.CreateUnbounded<string>();
    private int _disposeCount;

    public List<string> SentLines { get; } = [];
    public int DisposeCount => Volatile.Read(ref _disposeCount);
    public bool IsDisposed => DisposeCount > 0;
    public Task WaitForDisposeAsync() => _disposed.Task;
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
        if (Interlocked.Increment(ref _disposeCount) == 1)
        {
            _disposed.TrySetResult(true);
        }

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
