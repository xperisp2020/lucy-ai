namespace Lucy.Core.Auditing;

/// <summary>
/// Append-only audit persistence. Implementations must never update or delete
/// previously accepted events.
/// </summary>
public interface IAgentAuditStore
{
    Task AppendAsync(AgentAuditEvent auditEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentAuditEvent>> GetForTaskAsync(
        Guid taskId,
        int maximumCount,
        CancellationToken cancellationToken);
}
