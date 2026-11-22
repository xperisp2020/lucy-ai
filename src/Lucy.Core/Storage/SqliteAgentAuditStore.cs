using System.Globalization;
using Lucy.Core.Auditing;
using Microsoft.Data.Sqlite;

namespace Lucy.Core.Storage;

/// <summary>
/// SQLite audit log with database triggers that reject updates and deletes.
/// </summary>
public sealed class SqliteAgentAuditStore : IAgentAuditStore
{
    private readonly string _connectionString;

    private SqliteAgentAuditStore(string connectionString) =>
        _connectionString = connectionString;

    public static async Task<SqliteAgentAuditStore> CreateAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        if (string.Equals(fullPath, Path.GetPathRoot(fullPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The database file cannot be placed at a filesystem root.",
                nameof(databasePath));
        }

        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Database directory is invalid.", nameof(databasePath));
        Directory.CreateDirectory(directory);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        };

        var store = new SqliteAgentAuditStore(builder.ToString());
        await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return store;
    }

    public async Task AppendAsync(
        AgentAuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO agent_audit_events
                (id, task_id, event_type, occurred_at, tool_id, detail)
            VALUES
                ($id, $task_id, $event_type, $occurred_at, $tool_id, $detail);
            """;
        command.Parameters.AddWithValue("$id", auditEvent.Id.ToString("D"));
        command.Parameters.AddWithValue("$task_id", auditEvent.TaskId.ToString("D"));
        command.Parameters.AddWithValue("$event_type", (int)auditEvent.EventType);
        command.Parameters.AddWithValue(
            "$occurred_at",
            auditEvent.OccurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$tool_id", (object?)auditEvent.ToolId ?? DBNull.Value);
        command.Parameters.AddWithValue("$detail", (object?)auditEvent.Detail ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgentAuditEvent>> GetForTaskAsync(
        Guid taskId,
        int maximumCount,
        CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("Task identifier cannot be empty.", nameof(taskId));
        }

        if (maximumCount is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCount), "Maximum count must be between 1 and 1000.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, task_id, event_type, occurred_at, tool_id, detail
            FROM agent_audit_events
            WHERE task_id = $task_id
            ORDER BY sequence_number
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$task_id", taskId.ToString("D"));
        command.Parameters.AddWithValue("$limit", maximumCount);

        var events = new List<AgentAuditEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var eventTypeValue = reader.GetInt32(2);
            if (!Enum.IsDefined(typeof(AgentAuditEventType), eventTypeValue))
            {
                throw new InvalidDataException(
                    $"Stored audit event has unknown type {eventTypeValue}.");
            }

            var occurredAt = DateTimeOffset.ParseExact(
                reader.GetString(3), "O", CultureInfo.InvariantCulture, DateTimeStyles.None);

            events.Add(AgentAuditEvent.Rehydrate(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                (AgentAuditEventType)eventTypeValue,
                occurredAt,
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return events.AsReadOnly();
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS agent_audit_events (
                sequence_number INTEGER PRIMARY KEY AUTOINCREMENT,
                id TEXT NOT NULL UNIQUE,
                task_id TEXT NOT NULL,
                event_type INTEGER NOT NULL CHECK(event_type BETWEEN 0 AND 12),
                occurred_at TEXT NOT NULL,
                tool_id TEXT NULL CHECK(tool_id IS NULL OR length(tool_id) <= 128),
                detail TEXT NULL CHECK(detail IS NULL OR length(detail) <= 2000)
            );
            CREATE INDEX IF NOT EXISTS ix_agent_audit_task_sequence
                ON agent_audit_events(task_id, sequence_number);
            CREATE TRIGGER IF NOT EXISTS trg_agent_audit_no_update
                BEFORE UPDATE ON agent_audit_events
                BEGIN
                    SELECT RAISE(ABORT, 'audit events are append-only');
                END;
            CREATE TRIGGER IF NOT EXISTS trg_agent_audit_no_delete
                BEFORE DELETE ON agent_audit_events
                BEGIN
                    SELECT RAISE(ABORT, 'audit events are append-only');
                END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA busy_timeout = 5000;";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
