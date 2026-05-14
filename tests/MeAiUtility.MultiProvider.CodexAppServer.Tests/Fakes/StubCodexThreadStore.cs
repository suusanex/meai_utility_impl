using MeAiUtility.MultiProvider.CodexAppServer.Threading;

namespace MeAiUtility.MultiProvider.CodexAppServer.Tests.Fakes;

internal sealed class StubCodexThreadStore : ICodexThreadStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Dictionary<string, CodexThreadRecord>> _recordsByPath = new(StringComparer.Ordinal);

    public string? LastPathUsed { get; private set; }

    public Task<CodexThreadRecord?> TryGetByKeyAsync(string threadKey, string? threadStorePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPath = NormalizePath(threadStorePath);
        LastPathUsed = threadStorePath;

        lock (_lock)
        {
            if (!_recordsByPath.TryGetValue(normalizedPath, out var records)
                || !records.TryGetValue(threadKey, out var record))
            {
                return Task.FromResult<CodexThreadRecord?>(null);
            }

            return Task.FromResult<CodexThreadRecord?>(record);
        }
    }

    public Task<IReadOnlyList<CodexThreadRecord>> ListAsync(string? threadStorePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPath = NormalizePath(threadStorePath);
        LastPathUsed = threadStorePath;

        lock (_lock)
        {
            if (!_recordsByPath.TryGetValue(normalizedPath, out var records))
            {
                return Task.FromResult<IReadOnlyList<CodexThreadRecord>>([]);
            }

            var snapshot = records.Values
                .OrderByDescending(static record => record.LastUsedAt)
                .ThenBy(static record => record.ThreadKey, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult<IReadOnlyList<CodexThreadRecord>>(snapshot);
        }
    }

    public Task SaveAsync(CodexThreadRecord record, string? threadStorePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(record);

        var normalizedPath = NormalizePath(threadStorePath);
        LastPathUsed = threadStorePath;

        lock (_lock)
        {
            if (!_recordsByPath.TryGetValue(normalizedPath, out var records))
            {
                records = new Dictionary<string, CodexThreadRecord>(StringComparer.Ordinal);
                _recordsByPath[normalizedPath] = records;
            }

            records[record.ThreadKey] = record;
        }

        return Task.CompletedTask;
    }

    private static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? "__default__" : path.Trim();
}
