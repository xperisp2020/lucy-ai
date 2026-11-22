namespace Lucy.Core.Memory;

/// <summary>
/// User-inspectable memory with explicit provenance and bounded confidence.
/// Memory entries are data, not instructions or executable policy.
/// </summary>
public sealed record MemoryEntry
{
    private MemoryEntry(
        Guid id,
        MemoryKind kind,
        string content,
        string source,
        double? confidence,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Kind = kind;
        Content = content;
        Source = source;
        Confidence = confidence;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public MemoryKind Kind { get; }
    public string Content { get; }
    public string Source { get; }
    public double? Confidence { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }

    public static MemoryEntry Create(
        MemoryKind kind,
        string content,
        string source,
        double? confidence = null,
        DateTimeOffset? now = null)
    {
        Validate(kind, content, source, confidence);
        var timestamp = now ?? DateTimeOffset.UtcNow;
        return new MemoryEntry(
            Guid.NewGuid(), kind, content.Trim(), source.Trim(), confidence,
            timestamp, timestamp);
    }

    public MemoryEntry Update(
        string content,
        string source,
        double? confidence = null,
        DateTimeOffset? now = null)
    {
        Validate(Kind, content, source, confidence);
        var timestamp = now ?? DateTimeOffset.UtcNow;
        if (timestamp < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(now), "Memory timestamps cannot move backwards.");
        }

        return new MemoryEntry(
            Id, Kind, content.Trim(), source.Trim(), confidence, CreatedAt, timestamp);
    }

    internal static MemoryEntry Rehydrate(
        Guid id,
        MemoryKind kind,
        string content,
        string source,
        double? confidence,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty || updatedAt < createdAt)
        {
            throw new InvalidDataException("Stored memory identity or timestamps are invalid.");
        }

        Validate(kind, content, source, confidence);
        return new MemoryEntry(
            id, kind, content, source, confidence, createdAt, updatedAt);
    }

    private static void Validate(
        MemoryKind kind,
        string content,
        string source,
        double? confidence)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (content.Length > 20_000)
        {
            throw new ArgumentException("Memory content cannot exceed 20,000 characters.", nameof(content));
        }

        if (source.Length > 2_000)
        {
            throw new ArgumentException("Memory source cannot exceed 2,000 characters.", nameof(source));
        }

        if (confidence is < 0 or > 1 || (confidence.HasValue && double.IsNaN(confidence.Value)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidence), "Confidence must be between 0 and 1.");
        }
    }
}
