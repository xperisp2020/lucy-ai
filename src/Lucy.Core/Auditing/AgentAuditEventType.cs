namespace Lucy.Core.Auditing;

public enum AgentAuditEventType
{
    TaskCreated,
    PlanningStarted,
    PlanCreated,
    ExecutionStarted,
    ToolAuthorized,
    ToolDenied,
    ToolStarted,
    ToolSucceeded,
    ToolFailed,
    TaskCompleted,
    TaskFailed,
    TaskCancelled,
    TaskTimedOut
}
