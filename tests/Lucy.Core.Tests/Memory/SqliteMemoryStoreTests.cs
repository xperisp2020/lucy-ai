using Lucy.Core.Memory;
using Lucy.Core.Storage;

namespace Lucy.Core.Tests.Memory;

public sealed class SqliteMemoryStoreTests
{
    [Fact]
    public async Task SaveAndSearch_PersistsContentAndProvenance()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "memory.db");

        try
        {
            var store = await SqliteMemoryStore.CreateAsync(databasePath);
            var entry = MemoryEntry.Create(
                MemoryKind.Semantic,
                "Saturn has a complex ring system.",
                "NASA planetary science reference",
                confidence: 0.95,
                now: DateTimeOffset.Parse("2026-11-22T12:00:00Z"));

            await store.SaveAsync(entry, CancellationToken.None);

            var reopened = await SqliteMemoryStore.CreateAsync(databasePath);
            var results = await reopened.SearchAsync(
                "Saturn ring", MemoryKind.Semantic, 10, CancellationToken.None);

            var loaded = Assert.Single(results);
            Assert.Equal(entry.Id, loaded.Id);
            Assert.Equal(entry.Content, loaded.Content);
            Assert.Equal(entry.Source, loaded.Source);
            Assert.Equal(0.95, loaded.Confidence);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_UpdatesFtsIndexAndRejectsStaleSnapshot()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "memory.db");

        try
        {
            var store = await SqliteMemoryStore.CreateAsync(databasePath);
            var createdAt = DateTimeOffset.Parse("2026-11-22T12:00:00Z");
            var original = MemoryEntry.Create(
                MemoryKind.Episodic, "The blue bicycle was repaired.",
                "User-confirmed event", now: createdAt);
            var updated = original.Update(
                "The red bicycle was repaired.",
                "Corrected user-confirmed event",
                now: createdAt.AddSeconds(1));

            await store.SaveAsync(original, CancellationToken.None);
            await store.SaveAsync(updated, CancellationToken.None);

            var oldResults = await store.SearchAsync(
                "blue bicycle", null, 10, CancellationToken.None);
            var newResults = await store.SearchAsync(
                "red bicycle", null, 10, CancellationToken.None);

            Assert.Empty(oldResults);
            Assert.Equal(updated.Id, Assert.Single(newResults).Id);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.SaveAsync(original, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesMemoryFromSearchAndInspection()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "memory.db");

        try
        {
            var store = await SqliteMemoryStore.CreateAsync(databasePath);
            var entry = MemoryEntry.Create(
                MemoryKind.Procedural,
                "Always verify a backup before restoring it.",
                "Engineering procedure");

            await store.SaveAsync(entry, CancellationToken.None);

            Assert.True(await store.DeleteAsync(entry.Id, CancellationToken.None));
            Assert.False(await store.DeleteAsync(entry.Id, CancellationToken.None));
            Assert.Null(await store.GetByIdAsync(entry.Id, CancellationToken.None));
            Assert.Empty(await store.SearchAsync(
                "verify backup", null, 10, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetAllAsync_ReturnsInspectableEntriesWithinLimit()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "memory.db");

        try
        {
            var store = await SqliteMemoryStore.CreateAsync(databasePath);
            var start = DateTimeOffset.Parse("2026-11-22T12:00:00Z");

            for (var index = 0; index < 3; index++)
            {
                await store.SaveAsync(
                    MemoryEntry.Create(
                        MemoryKind.Episodic,
                        $"Recorded event number {index}.",
                        "Test fixture",
                        now: start.AddSeconds(index)),
                    CancellationToken.None);
            }

            var entries = await store.GetAllAsync(2, CancellationToken.None);

            Assert.Equal(2, entries.Count);
            Assert.Contains("number 2", entries[0].Content, StringComparison.Ordinal);
            Assert.Contains("number 1", entries[1].Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Create_RejectsInvalidConfidenceAndEmptyProvenance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MemoryEntry.Create(
            MemoryKind.Semantic, "A fact", "A source", confidence: 1.1));

        Assert.Throws<ArgumentException>(() => MemoryEntry.Create(
            MemoryKind.Semantic, "A fact", " "));
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lucy-memory-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
