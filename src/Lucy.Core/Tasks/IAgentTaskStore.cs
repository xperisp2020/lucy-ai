namespace Lucy.Core.Tasks;

/// <summary>
/// Persistence boundary for task snapshots. Implementations must persist each
/// accepted snapshot atomically and must not silently discard failures.
/// </summary>
public interface IAgentTaskStore
{
    Task SaveAsync(AgentTask task, CancellationToken cancellationToken);
}
