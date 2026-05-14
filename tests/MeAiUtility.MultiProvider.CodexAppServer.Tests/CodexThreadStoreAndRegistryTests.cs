using MeAiUtility.MultiProvider.CodexAppServer.Options;
using MeAiUtility.MultiProvider.CodexAppServer.Tests.Fakes;
using MeAiUtility.MultiProvider.CodexAppServer.Threading;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.CodexAppServer.Tests;

public class CodexThreadStoreAndRegistryTests
{
    [Test]
    public async Task FileCodexThreadStore_PersistsRecordsAcrossInstances()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "MeAiUtility.CodexAppServer.Tests", Guid.NewGuid().ToString("N"));
        var storePath = Path.Combine(rootDirectory, "threads.json");

        try
        {
            var firstStore = new FileCodexThreadStore(
                new CodexAppServerProviderOptions { ThreadStorePath = storePath },
                NullLogger<FileCodexThreadStore>.Instance);

            var createdAt = DateTimeOffset.UtcNow.AddMinutes(-10);
            var lastUsedAt = DateTimeOffset.UtcNow;
            await firstStore.SaveAsync(
                new CodexThreadRecord(
                    "meai:key:store",
                    "thread-store",
                    "store-thread",
                    @"D:\repo",
                    "gpt-5.5-codex",
                    createdAt,
                    lastUsedAt),
                null,
                CancellationToken.None);

            var secondStore = new FileCodexThreadStore(
                new CodexAppServerProviderOptions { ThreadStorePath = storePath },
                NullLogger<FileCodexThreadStore>.Instance);

            var loaded = await secondStore.TryGetByKeyAsync("meai:key:store", null, CancellationToken.None);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.ThreadId, Is.EqualTo("thread-store"));
            Assert.That(loaded.ThreadName, Is.EqualTo("store-thread"));

            var list = await secondStore.ListAsync(null, CancellationToken.None);
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].ThreadKey, Is.EqualTo("meai:key:store"));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Test]
    public async Task CodexThreadRegistry_ListsAndGetsRecordsByThreadKey()
    {
        var store = new StubCodexThreadStore();
        await store.SaveAsync(
            new CodexThreadRecord(
                "alpha",
                "thread-alpha",
                "alpha-name",
                @"D:\a",
                "gpt-5.5-codex",
                DateTimeOffset.UtcNow.AddHours(-2),
                DateTimeOffset.UtcNow.AddMinutes(-1)),
            @"D:\tmp\registry.json",
            CancellationToken.None);

        await store.SaveAsync(
            new CodexThreadRecord(
                "beta",
                "thread-beta",
                "beta-name",
                @"D:\b",
                "gpt-5.5-codex",
                DateTimeOffset.UtcNow.AddHours(-1),
                DateTimeOffset.UtcNow.AddMinutes(-2)),
            @"D:\tmp\registry.json",
            CancellationToken.None);

        var registry = new CodexThreadRegistry(store);
        var list = await registry.ListAsync(@"D:\tmp\registry.json", CancellationToken.None);
        Assert.That(list.Count, Is.EqualTo(2));
        Assert.That(list[0].ThreadKey, Is.EqualTo("alpha"));
        Assert.That(store.LastPathUsed, Is.EqualTo(@"D:\tmp\registry.json"));

        var existing = await registry.TryGetByThreadKeyAsync("beta", @"D:\tmp\registry.json", CancellationToken.None);
        Assert.That(existing, Is.Not.Null);
        Assert.That(existing!.ThreadId, Is.EqualTo("thread-beta"));

        var missing = await registry.TryGetByThreadKeyAsync("missing", @"D:\tmp\registry.json", CancellationToken.None);
        Assert.That(missing, Is.Null);
    }
}
