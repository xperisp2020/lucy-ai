using System.Globalization;
using Lucy.Core.Tasks;
using Microsoft.Data.Sqlite;

namespace Lucy.Core.Storage;

/// <summary>
/// SQLite-backed task snapshots. Each operation uses its own connection so
/// instances can safely be shared across asynchronous callers.
/// </summary>
public sealed class SqliteAgentTaskRepository : IAgentTaskRepository
{
    private readonly string _connectionString;

    private SqliteAgentTaskRepository(string connectionString) =>
        _connectionString = connectionString;

    public static async Task<SqliteAgentTaskRepository> CreateAsync(
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
            Pooling = true
        };

        var repository = new SqliteAgentTaskRepository(builder.ToString());
        await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return repository;
    }

    public async Task SaveAsync(
        AgentTask task,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO agent_tasks
                (id, goal, status, created_at, updated_at, failure_reason)
            VALUES
                ($id, $goal, $status, $created_at, $updated_at, $failure_reason)
            ON CONFLICT(id) DO UPDATE SET
                status = excluded.status,
                updated_at = excluded.updated_at,
                failure_reason = excluded.failure_reason
            WHERE agent_tasks.goal = excluded.goal
              AND agent_tasks.created_at = excluded.created_at
              AND agent_tasks.updated_at <= excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$id", task.Id.ToString("D"));
        command.Parameters.AddWithValue("$goal", task.Goal);
        command.Parameters.AddWithValue("$status", (int)task.Status);
        command.Parameters.AddWithValue("$created_at", task.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated_at", task.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$failure_reason", (object?)task.FailureReason ?? DBNull.Value);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);
        if (affected != 1)
        {
            throw new InvalidOperationException(
                $"Task '{task.Id}' was not saved because its identity or timestamp conflicts with stored data.");
        }
    }

    public async Task<AgentTask?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadTask(reader)
            : null;
    }

    public async Task<IReadOnlyList<AgentTask>> GetRecentAsync(
        int maximumCount,
        CancellationToken cancellationToken)
    {
        if (maximumCount is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCount), "Maximum count must be between 1 and 500.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " ORDER BY updated_at DESC, id LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", maximumCount);

        var tasks = new List<AgentTask>(maximumCount);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tasks.Add(ReadTask(reader));
        }

        return tasks.AsReadOnly();
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS agent_tasks (
                id TEXT NOT NULL PRIMARY KEY,
                goal TEXT NOT NULL CHECK(length(trim(goal)) > 0),
                status INTEGER NOT NULL CHECK(status BETWEEN 0 AND 6),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                failure_reason TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_agent_tasks_updated_at
                ON agent_tasks(updated_at DESC);
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER NOT NULL PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            INSERT OR IGNORE INTO schema_migrations(version, applied_at)
                VALUES (1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            PRAGMA user_version = 1;
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
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static AgentTask ReadTask(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var goal = reader.GetString(1);
        var statusValue = reader.GetInt32(2);
        if (!Enum.IsDefined(typeof(AgentTaskStatus), statusValue))
        {
            throw new InvalidDataException($"Stored task '{id}' has unknown status {statusValue}.");
        }

        var createdAt = DateTimeOffset.Parse(
            reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var updatedAt = DateTimeOffset.Parse(
            reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var failureReason = reader.IsDBNull(5) ? null : reader.GetString(5);

        return AgentTask.Rehydrate(
            id, goal, (AgentTaskStatus)statusValue, createdAt, updatedAt, failureReason);
    }

    private const string SelectColumns =
        "SELECT id, goal, status, created_at, updated_at, failure_reason FROM agent_tasks";
}
