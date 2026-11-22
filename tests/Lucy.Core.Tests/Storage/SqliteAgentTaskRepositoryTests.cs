using Lucy.Core.Storage;
using Lucy.Core.Tasks;

namespace Lucy.Core.Tests.Storage;

public sealed class SqliteAgentTaskRepositoryTests
{
    [Fact]
    public async Task SaveAndGet_PersistsTaskAcrossRepositoryInstances()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "tasks.db");

        try
        {
            var repository = await SqliteAgentTaskRepository.CreateAsync(databasePath);
            var createdAt = DateTimeOffset.Parse("2026-11-22T12:00:00Z");
            var task = AgentTask.Create("persist this task", createdAt);
            var planning = task.TransitionTo(AgentTaskStatus.Planning, createdAt.AddSeconds(1));
            var running = planning.TransitionTo(AgentTaskStatus.Running, createdAt.AddSeconds(2));

            await repository.SaveAsync(task, CancellationToken.None);
            await repository.SaveAsync(planning, CancellationToken.None);
            await repository.SaveAsync(running, CancellationToken.None);

            var reopened = await SqliteAgentTaskRepository.CreateAsync(databasePath);
            var loaded = await reopened.GetByIdAsync(task.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(task.Id, loaded.Id);
            Assert.Equal(task.Goal, loaded.Goal);
            Assert.Equal(AgentTaskStatus.Running, loaded.Status);
            Assert.Equal(createdAt, loaded.CreatedAt);
            Assert.Equal(createdAt.AddSeconds(2), loaded.UpdatedAt);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_RejectsStaleSnapshot()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "tasks.db");

        try
        {
            var repository = await SqliteAgentTaskRepository.CreateAsync(databasePath);
            var createdAt = DateTimeOffset.Parse("2026-11-22T12:00:00Z");
            var pending = AgentTask.Create("stale write", createdAt);
            var planning = pending.TransitionTo(AgentTaskStatus.Planning, createdAt.AddSeconds(1));

            await repository.SaveAsync(pending, CancellationToken.None);
            await repository.SaveAsync(planning, CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.SaveAsync(pending, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestTasksWithinRequestedLimit()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "tasks.db");

        try
        {
            var repository = await SqliteAgentTaskRepository.CreateAsync(databasePath);
            var start = DateTimeOffset.Parse("2026-11-22T12:00:00Z");
            var tasks = Enumerable.Range(0, 3)
                .Select(index => AgentTask.Create($"task {index}", start.AddSeconds(index)))
                .ToArray();

            foreach (var task in tasks)
            {
                await repository.SaveAsync(task, CancellationToken.None);
            }

            var recent = await repository.GetRecentAsync(2, CancellationToken.None);

            Assert.Equal(2, recent.Count);
            Assert.Equal(tasks[2].Id, recent[0].Id);
            Assert.Equal(tasks[1].Id, recent[1].Id);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetRecentAsync_RejectsUnboundedLimit()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "tasks.db");

        try
        {
            var repository = await SqliteAgentTaskRepository.CreateAsync(databasePath);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => repository.GetRecentAsync(501, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lucy-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
