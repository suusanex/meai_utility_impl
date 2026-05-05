namespace MeAiUtility.MultiProvider.CodexAppServer.Abstractions;

public interface ICodexTransport : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task SendLineAsync(string line, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken = default);
}
