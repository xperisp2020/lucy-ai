using Lucy.Core.Auditing;
using Lucy.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Lucy.Core.Tests.Auditing;

public sealed class SqliteAgentAuditStoreTests
{
    [Fact]
    public async Task AppendAndRead_PersistsEventsInAppendOrder()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "audit.db");

        try
        {
            var taskId = Guid.NewGuid();
            var first = AgentAuditEvent.Create(
                taskId, AgentAuditEventType.TaskCreated,
                occurredAt: DateTimeOffset.Parse("2026-11-22T12:00:00Z"));
            var second = AgentAuditEvent.Create(
                taskId, AgentAuditEventType.PlanningStarted,
                detail: "planning",
                occurredAt: DateTimeOffset.Parse("2026-11-22T12:00:01Z"));

            var store = await SqliteAgentAuditStore.CreateAsync(databasePath);
            await store.AppendAsync(first, CancellationToken.None);
            await store.AppendAsync(second, CancellationToken.None);

            var reopened = await SqliteAgentAuditStore.CreateAsync(databasePath);
            var events = await reopened.GetForTaskAsync(taskId, 10, CancellationToken.None);

            Assert.Equal(2, events.Count);
            Assert.Equal(first.Id, events[0].Id);
            Assert.Equal(second.Id, events[1].Id);
            Assert.Equal(AgentAuditEventType.TaskCreated, events[0].EventType);
            Assert.Equal(AgentAuditEventType.PlanningStarted, events[1].EventType);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DatabaseTriggers_RejectAuditMutationAndDeletion()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "audit.db");

        try
        {
            var taskId = Guid.NewGuid();
            var store = await SqliteAgentAuditStore.CreateAsync(databasePath);
            var auditEvent = AgentAuditEvent.Create(taskId, AgentAuditEventType.TaskCreated);
            await store.AppendAsync(auditEvent, CancellationToken.None);

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            };

            await using var connection = new SqliteConnection(builder.ToString());
            await connection.OpenAsync();

            await using var update = connection.CreateCommand();
            update.CommandText = "UPDATE agent_audit_events SET detail = 'tampered' WHERE id = $id;";
            update.Parameters.AddWithValue("$id", auditEvent.Id.ToString("D"));
            await Assert.ThrowsAsync<SqliteException>(async () =>
            {
                await update.ExecuteNonQueryAsync();
            });

            await using var delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM agent_audit_events WHERE id = $id;";
            delete.Parameters.AddWithValue("$id", auditEvent.Id.ToString("D"));
            await Assert.ThrowsAsync<SqliteException>(() => delete.ExecuteNonQuery());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Create_RejectsOversizedAuditDetail()
    {
        Assert.Throws<ArgumentException>(() => AgentAuditEvent.Create(
            Guid.NewGuid(),
            AgentAuditEventType.TaskFailed,
            detail: new string('x', 2001)));
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lucy-audit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
