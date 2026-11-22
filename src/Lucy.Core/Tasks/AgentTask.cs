namespace Lucy.Core.Tasks;

/// <summary>
/// Immutable snapshot of a task. State changes create a new snapshot so callers
/// cannot mutate task state without passing through the transition rules.
/// </summary>
public sealed record AgentTask
{
    private AgentTask(
        Guid id,
        string goal,
        AgentTaskStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? failureReason)
    {
        Id = id;
        Goal = goal;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        FailureReason = failureReason;
    }

    public Guid Id { get; }
    public string Goal { get; }
    public AgentTaskStatus Status { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public string? FailureReason { get; }

    internal static AgentTask Rehydrate(
        Guid id,
        string goal,
        AgentTaskStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? failureReason)
    {
        if (id == Guid.Empty)
        {
            throw new InvalidDataException("Stored task identifier cannot be empty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        if (!Enum.IsDefined(status))
        {
            throw new InvalidDataException($"Stored task status '{status}' is invalid.");
        }

        if (updatedAt < createdAt)
        {
            throw new InvalidDataException("Stored task timestamps are inconsistent.");
        }

        if (status == AgentTaskStatus.Failed && string.IsNullOrWhiteSpace(failureReason))
        {
            throw new InvalidDataException("Failed tasks must contain a failure reason.");
        }

        return new AgentTask(id, goal, status, createdAt, updatedAt, failureReason);
    }

    public static AgentTask Create(string goal, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        var timestamp = now ?? DateTimeOffset.UtcNow;
        return new AgentTask(Guid.NewGuid(), goal.Trim(), AgentTaskStatus.Pending,
            timestamp, timestamp, null);
    }

    public AgentTask TransitionTo(
        AgentTaskStatus nextStatus,
        DateTimeOffset? now = null,
        string? failureReason = null)
    {
        if (!IsAllowedTransition(Status, nextStatus))
        {
            throw new InvalidOperationException(
                $"Task cannot transition from {Status} to {nextStatus}.");
        }

        if (nextStatus == AgentTaskStatus.Failed &&
            string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException(
                "A failure reason is required when failing a task.",
                nameof(failureReason));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;
        if (timestamp < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(now), "Task timestamps cannot move backwards.");
        }

        return new AgentTask(
            Id,
            Goal,
            nextStatus,
            CreatedAt,
            timestamp,
            nextStatus == AgentTaskStatus.Failed ? failureReason!.Trim() : null);
    }

    private static bool IsAllowedTransition(
        AgentTaskStatus current,
        AgentTaskStatus next) =>
        (current, next) switch
        {
            (AgentTaskStatus.Pending, AgentTaskStatus.Planning) => true,
            (AgentTaskStatus.Pending, AgentTaskStatus.Cancelled) => true,
            (AgentTaskStatus.Planning, AgentTaskStatus.Running) => true,
            (AgentTaskStatus.Planning, AgentTaskStatus.Failed) => true,
            (AgentTaskStatus.Planning, AgentTaskStatus.Cancelled) => true,
            (AgentTaskStatus.Planning, AgentTaskStatus.TimedOut) => true,
            (AgentTaskStatus.Running, AgentTaskStatus.Completed) => true,
            (AgentTaskStatus.Running, AgentTaskStatus.Failed) => true,
            (AgentTaskStatus.Running, AgentTaskStatus.Cancelled) => true,
            (AgentTaskStatus.Running, AgentTaskStatus.TimedOut) => true,
            _ => false
        };
}
