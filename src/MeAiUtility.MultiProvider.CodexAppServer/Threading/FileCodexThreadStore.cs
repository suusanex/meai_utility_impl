using System.Text.Json;
using MeAiUtility.MultiProvider.CodexAppServer.Options;
using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.CodexAppServer.Threading;

internal sealed class FileCodexThreadStore(
    CodexAppServerProviderOptions options,
    ILogger<FileCodexThreadStore> logger) : ICodexThreadStore
{
    private const string ProviderName = "CodexAppServer";
    // TODO: 現在は SemaphoreSlim による同一プロセス内排他のみ対応。cross-process lock は未対応のため、
    // 必要に応じて named mutex / lock file の導入を検討すること。
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private readonly string? _configuredStorePath = NormalizeOptional(options.ThreadStorePath);

    public async Task<CodexThreadRecord?> TryGetByKeyAsync(string threadKey, string? threadStorePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(threadKey))
        {
            throw new InvalidRequestException("ThreadKey must be a non-empty string.", ProviderName);
        }

        var normalizedThreadKey = threadKey.Trim();
        var resolvedPath = ResolveStorePath(threadStorePath);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(resolvedPath, cancellationToken);
            return document.Threads.FirstOrDefault(record => string.Equals(record.ThreadKey, normalizedThreadKey, StringComparison.Ordinal));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<CodexThreadRecord>> ListAsync(string? threadStorePath, CancellationToken cancellationToken)
    {
        var resolvedPath = ResolveStorePath(threadStorePath);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(resolvedPath, cancellationToken);
            return document.Threads
                .OrderByDescending(static record => record.LastUsedAt)
                .ThenBy(static record => record.ThreadKey, StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CodexThreadRecord record, string? threadStorePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.ThreadKey))
        {
            throw new InvalidRequestException("ThreadKey must be a non-empty string.", ProviderName);
        }

        if (string.IsNullOrWhiteSpace(record.ThreadId))
        {
            throw new InvalidRequestException("ThreadId must be a non-empty string.", ProviderName);
        }

        var normalizedRecord = NormalizeRecord(record);
        var resolvedPath = ResolveStorePath(threadStorePath);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(resolvedPath, cancellationToken);
            var index = document.Threads.FindIndex(existing => string.Equals(existing.ThreadKey, normalizedRecord.ThreadKey, StringComparison.Ordinal));
            if (index >= 0)
            {
                var existing = document.Threads[index];
                var mergedRecord = normalizedRecord with
                {
                    CreatedAt = normalizedRecord.CreatedAt == default ? existing.CreatedAt : normalizedRecord.CreatedAt,
                    LastUsedAt = normalizedRecord.LastUsedAt == default ? DateTimeOffset.UtcNow : normalizedRecord.LastUsedAt,
                };
                document.Threads[index] = mergedRecord;
            }
            else
            {
                document.Threads.Add(normalizedRecord with
                {
                    CreatedAt = normalizedRecord.CreatedAt == default ? DateTimeOffset.UtcNow : normalizedRecord.CreatedAt,
                    LastUsedAt = normalizedRecord.LastUsedAt == default ? DateTimeOffset.UtcNow : normalizedRecord.LastUsedAt,
                });
            }

            await WriteDocumentAsync(resolvedPath, document, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ResolveStorePath(string? threadStorePath)
    {
        var explicitPath = NormalizeOptional(threadStorePath) ?? _configuredStorePath;
        if (explicitPath is not null)
        {
            return explicitPath;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new ProviderException("Failed to resolve LocalApplicationData for codex thread store.", ProviderName);
        }

        return Path.Combine(localAppData, "MeAiUtility", "CodexAppServer", "threads.json");
    }

    private async Task<ThreadStoreDocument> ReadDocumentAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new ThreadStoreDocument();
        }

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var document = await JsonSerializer.DeserializeAsync<ThreadStoreDocument>(stream, _jsonOptions, cancellationToken);
            return document ?? new ThreadStoreDocument();
        }
        catch (JsonException ex)
        {
            logger.LogError("Failed to parse codex thread store. Exception={Exception}", ex.ToString());
            throw new ProviderException($"Failed to parse codex thread store '{path}'.", ProviderName, null, null, null, ex);
        }
        catch (IOException ex)
        {
            logger.LogError("Failed to read codex thread store. Exception={Exception}", ex.ToString());
            throw new ProviderException($"Failed to read codex thread store '{path}'.", ProviderName, null, null, null, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError("Failed to access codex thread store. Exception={Exception}", ex.ToString());
            throw new ProviderException($"Access denied for codex thread store '{path}'.", ProviderName, null, null, null, ex);
        }
        catch (System.NotSupportedException ex)
        {
            logger.LogError("Invalid codex thread store path. Exception={Exception}", ex.ToString());
            throw new ProviderException($"Invalid codex thread store path '{path}'.", ProviderName, null, null, null, ex);
        }
    }

    private async Task WriteDocumentAsync(string path, ThreadStoreDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, document, _jsonOptions, cancellationToken);
            }

            if (File.Exists(path))
            {
                File.Move(temporaryPath, path, overwrite: true);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        catch (IOException ex)
        {
            logger.LogError("Failed to write codex thread store. Exception={Exception}", ex.ToString());
            throw new ProviderException($"Failed to write codex thread store '{path}'.", ProviderName, null, null, null, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError("Failed to access codex thread store while writing. Exception={Exception}", ex.ToString());
            throw new ProviderException($"Access denied for codex thread store '{path}'.", ProviderName, null, null, null, ex);
        }
        catch (System.NotSupportedException ex)
        {
            logger.LogError("Invalid codex thread store path while writing. Exception={Exception}", ex.ToString());
            throw new ProviderException($"Invalid codex thread store path '{path}'.", ProviderName, null, null, null, ex);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException ex)
                {
                    logger.LogError("Failed to delete temporary codex thread store file. Exception={Exception}", ex.ToString());
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.LogError("Failed to access temporary codex thread store file. Exception={Exception}", ex.ToString());
                }
            }
        }
    }

    private static CodexThreadRecord NormalizeRecord(CodexThreadRecord record)
        => record with
        {
            ThreadKey = record.ThreadKey.Trim(),
            ThreadId = record.ThreadId.Trim(),
            ThreadName = NormalizeOptional(record.ThreadName),
            WorkingDirectory = NormalizeOptional(record.WorkingDirectory),
            ModelId = NormalizeOptional(record.ModelId),
        };

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class ThreadStoreDocument
    {
        public List<CodexThreadRecord> Threads { get; set; } = [];
    }
}
