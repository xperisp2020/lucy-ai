using System.Globalization;
using Lucy.Core.Memory;
using Microsoft.Data.Sqlite;

namespace Lucy.Core.Storage;

/// <summary>
/// SQLite memory store using FTS5 for provenance-aware lexical retrieval.
/// Vector embeddings are intentionally deferred until benchmarks justify them.
/// </summary>
public sealed class SqliteMemoryStore : IMemoryStore
{
    private readonly string _connectionString;

    private SqliteMemoryStore(string connectionString) =>
        _connectionString = connectionString;

    public static async Task<SqliteMemoryStore> CreateAsync(
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

        var store = new SqliteMemoryStore(builder.ToString());
        await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return store;
    }

    public async Task SaveAsync(
        MemoryEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO memory_entries
                (id, kind, content, source, confidence, created_at, updated_at)
            VALUES
                ($id, $kind, $content, $source, $confidence, $created_at, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                kind = excluded.kind,
                content = excluded.content,
                source = excluded.source,
                confidence = excluded.confidence,
                updated_at = excluded.updated_at
            WHERE memory_entries.created_at = excluded.created_at
              AND memory_entries.updated_at <= excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$id", entry.Id.ToString("D"));
        command.Parameters.AddWithValue("$kind", (int)entry.Kind);
        command.Parameters.AddWithValue("$content", entry.Content);
        command.Parameters.AddWithValue("$source", entry.Source);
        command.Parameters.AddWithValue("$confidence", (object?)entry.Confidence ?? DBNull.Value);
        command.Parameters.AddWithValue("$created_at", entry.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated_at", entry.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

        var affected = await command.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);
        if (affected != 1)
        {
            throw new InvalidOperationException(
                $"Memory entry '{entry.Id}' was not saved because its identity or timestamp conflicts with stored data.");
        }
    }

    public async Task<MemoryEntry?> GetByIdAsync(
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
            ? ReadEntry(reader)
            : null;
    }

    public async Task<IReadOnlyList<MemoryEntry>> SearchAsync(
        string query,
        MemoryKind? kind,
        int maximumCount,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ValidateLimit(maximumCount);

        if (kind.HasValue && !Enum.IsDefined(kind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length is 0 or > 20 || terms.Any(term => term.Length > 64))
        {
            throw new ArgumentException(
                "Search must contain between 1 and 20 terms; each term is limited to 64 characters.",
                nameof(query));
        }

        var matchExpression = string.Join(
            " AND ",
            terms.Select(term => $"\"{term.Replace("\"", "\"\"", StringComparison.Ordinal)}\""));

        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.id, e.kind, e.content, e.source, e.confidence, e.created_at, e.updated_at
            FROM memory_entries_fts
            INNER JOIN memory_entries AS e ON e.id = f.id
            WHERE memory_entries_fts MATCH $query
              AND ($kind IS NULL OR e.kind = $kind)
            ORDER BY bm25(memory_entries_fts), e.updated_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$query", matchExpression);
        command.Parameters.AddWithValue("$kind", kind.HasValue ? (object)(int)kind.Value : DBNull.Value);
        command.Parameters.AddWithValue("$limit", maximumCount);

        return await ReadEntriesAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MemoryEntry>> GetAllAsync(
        int maximumCount,
        CancellationToken cancellationToken)
    {
        ValidateLimit(maximumCount);

        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " ORDER BY updated_at DESC, id LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", maximumCount);

        return await ReadEntriesAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM memory_entries WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS memory_entries (
                id TEXT NOT NULL PRIMARY KEY,
                kind INTEGER NOT NULL CHECK(kind BETWEEN 0 AND 2),
                content TEXT NOT NULL CHECK(length(trim(content)) BETWEEN 1 AND 20000),
                source TEXT NOT NULL CHECK(length(trim(source)) BETWEEN 1 AND 2000),
                confidence REAL NULL CHECK(confidence IS NULL OR (confidence >= 0 AND confidence <= 1)),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_memory_entries_updated_at
                ON memory_entries(updated_at DESC);
            CREATE VIRTUAL TABLE IF NOT EXISTS memory_entries_fts
                USING fts5(id UNINDEXED, content, source, tokenize='unicode61');
            CREATE TRIGGER IF NOT EXISTS trg_memory_entries_insert
                AFTER INSERT ON memory_entries
                BEGIN
                    INSERT INTO memory_entries_fts(id, content, source)
                    VALUES (new.id, new.content, new.source);
                END;
            CREATE TRIGGER IF NOT EXISTS trg_memory_entries_update
                AFTER UPDATE ON memory_entries
                BEGIN
                    DELETE FROM memory_entries_fts WHERE id = old.id;
                    INSERT INTO memory_entries_fts(id, content, source)
                    VALUES (new.id, new.content, new.source);
                END;
            CREATE TRIGGER IF NOT EXISTS trg_memory_entries_delete
                AFTER DELETE ON memory_entries
                BEGIN
                    DELETE FROM memory_entries_fts WHERE id = old.id;
                END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<MemoryEntry>> ReadEntriesAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var entries = new List<MemoryEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(ReadEntry(reader));
        }

        return entries.AsReadOnly();
    }

    private static MemoryEntry ReadEntry(SqliteDataReader reader)
    {
        var kindValue = reader.GetInt32(1);
        if (!Enum.IsDefined(typeof(MemoryKind), kindValue))
        {
            throw new InvalidDataException($"Stored memory has unknown kind {kindValue}.");
        }

        var confidence = reader.IsDBNull(4) ? null : reader.GetDouble(4);
        var createdAt = DateTimeOffset.ParseExact(
            reader.GetString(5), "O", CultureInfo.InvariantCulture, DateTimeStyles.None);
        var updatedAt = DateTimeOffset.ParseExact(
            reader.GetString(6), "O", CultureInfo.InvariantCulture, DateTimeStyles.None);

        return MemoryEntry.Rehydrate(
            Guid.Parse(reader.GetString(0)),
            (MemoryKind)kindValue,
            reader.GetString(2),
            reader.GetString(3),
            confidence,
            createdAt,
            updatedAt);
    }

    private static void ValidateLimit(int maximumCount)
    {
        if (maximumCount is < 1 or > 5000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCount), "Maximum count must be between 1 and 5000.");
        }
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

    private const string SelectColumns =
        "SELECT id, kind, content, source, confidence, created_at, updated_at FROM memory_entries";
}
