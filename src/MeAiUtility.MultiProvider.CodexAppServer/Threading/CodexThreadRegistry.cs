namespace MeAiUtility.MultiProvider.CodexAppServer.Threading;

internal sealed class CodexThreadRegistry(ICodexThreadStore threadStore) : ICodexThreadRegistry
{
    public async Task<IReadOnlyList<CodexThreadDescriptor>> ListAsync(string? threadStorePath = null, CancellationToken cancellationToken = default)
    {
        var records = await threadStore.ListAsync(threadStorePath, cancellationToken);
        return records
            .OrderByDescending(static record => record.LastUsedAt)
            .ThenBy(static record => record.ThreadKey, StringComparer.Ordinal)
            .Select(static record => new CodexThreadDescriptor(
                record.ThreadKey,
                record.ThreadId,
                record.ThreadName,
                record.WorkingDirectory,
                record.ModelId,
                record.CreatedAt,
                record.LastUsedAt))
            .ToArray();
    }

    public async Task<CodexThreadDescriptor?> TryGetByThreadKeyAsync(string threadKey, string? threadStorePath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadKey))
        {
            throw new ArgumentException("threadKey must be a non-empty string.", nameof(threadKey));
        }

        var normalizedThreadKey = threadKey.Trim();
        var record = await threadStore.TryGetByKeyAsync(normalizedThreadKey, threadStorePath, cancellationToken);
        if (record is null)
        {
            return null;
        }

        return new CodexThreadDescriptor(
            record.ThreadKey,
            record.ThreadId,
            record.ThreadName,
            record.WorkingDirectory,
            record.ModelId,
            record.CreatedAt,
            record.LastUsedAt);
    }
}
