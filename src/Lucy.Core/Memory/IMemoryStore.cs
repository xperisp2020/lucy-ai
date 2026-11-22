namespace Lucy.Core.Memory;

/// <summary>
/// Inspectable memory storage with bounded retrieval and explicit deletion.
/// </summary>
public interface IMemoryStore
{
    Task SaveAsync(MemoryEntry entry, CancellationToken cancellationToken);

    Task<MemoryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemoryEntry>> SearchAsync(
        string query,
        MemoryKind? kind,
        int maximumCount,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MemoryEntry>> GetAllAsync(
        int maximumCount,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
