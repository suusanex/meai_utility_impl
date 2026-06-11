using MultiCodingAgentFacade.CodexAppServer.Threading;

namespace MultiCodingAgentFacade.CodexAppServer.Tests.Fakes;

internal sealed class StubCodexThreadStore : ICodexThreadStore
{
    private readonly Dictionary<string, CodexThreadRecord> records = new(StringComparer.Ordinal);

    public Task<CodexThreadRecord?> TryGetByKeyAsync(string threadKey, string? threadStorePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(records.TryGetValue(threadKey, out var record) ? record : null);
    }

    public Task<IReadOnlyList<CodexThreadRecord>> ListAsync(string? threadStorePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<CodexThreadRecord>>(records.Values.ToArray());
    }

    public Task SaveAsync(CodexThreadRecord record, string? threadStorePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        records[record.ThreadKey] = record;
        return Task.CompletedTask;
    }
}
