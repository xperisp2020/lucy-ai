namespace Lucy.Core.Auditing;

/// <summary>
/// Minimal, append-only audit record. Details must not contain credentials,
/// provider prompts, or unredacted tool output.
/// </summary>
public sealed record AgentAuditEvent
{
    private AgentAuditEvent(
        Guid id,
        Guid taskId,
        AgentAuditEventType eventType,
        DateTimeOffset occurredAt,
        string? toolId,
        string? detail)
    {
        Id = id;
        TaskId = taskId;
        EventType = eventType;
        OccurredAt = occurredAt;
        ToolId = toolId;
        Detail = detail;
    }

    public Guid Id { get; }
    public Guid TaskId { get; }
    public AgentAuditEventType EventType { get; }
    public DateTimeOffset OccurredAt { get; }
    public string? ToolId { get; }
    public string? Detail { get; }

    public static AgentAuditEvent Create(
        Guid taskId,
        AgentAuditEventType eventType,
        string? toolId = null,
        string? detail = null,
        DateTimeOffset? occurredAt = null)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("Task identifier cannot be empty.", nameof(taskId));
        }

        if (!Enum.IsDefined(eventType))
        {
            throw new ArgumentOutOfRangeException(nameof(eventType));
        }

        if (toolId is { Length: > 128 })
        {
            throw new ArgumentException("Tool identifier cannot exceed 128 characters.", nameof(toolId));
        }

        if (detail is { Length: > 2000 })
        {
            throw new ArgumentException("Audit detail cannot exceed 2000 characters.", nameof(detail));
        }

        return new AgentAuditEvent(
            Guid.NewGuid(),
            taskId,
            eventType,
            occurredAt ?? DateTimeOffset.UtcNow,
            toolId,
            detail);
    }

    internal static AgentAuditEvent Rehydrate(
        Guid id,
        Guid taskId,
        AgentAuditEventType eventType,
        DateTimeOffset occurredAt,
        string? toolId,
        string? detail)
    {
        if (id == Guid.Empty || taskId == Guid.Empty || !Enum.IsDefined(eventType))
        {
            throw new InvalidDataException("Stored audit event identity or type is invalid.");
        }

        if (toolId is { Length: > 128 } || detail is { Length: > 2000 })
        {
            throw new InvalidDataException("Stored audit event exceeds supported field limits.");
        }

        return new AgentAuditEvent(id, taskId, eventType, occurredAt, toolId, detail);
    }
}
