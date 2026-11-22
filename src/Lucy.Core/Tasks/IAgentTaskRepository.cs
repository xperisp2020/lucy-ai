namespace Lucy.Core.Tasks;

/// <summary>
/// Durable task repository with lookup support for recovery and inspection.
/// </summary>
public interface IAgentTaskRepository : IAgentTaskStore
{
    Task<AgentTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentTask>> GetRecentAsync(
        int maximumCount,
        CancellationToken cancellationToken);
}
