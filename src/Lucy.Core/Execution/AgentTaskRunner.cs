using Lucy.Core.Auditing;
using Lucy.Core.Planning;
using Lucy.Core.Policy;
using Lucy.Core.Tasks;
using Lucy.Core.Tools;

namespace Lucy.Core.Execution;

/// <summary>
/// Runs a planner-produced sequence with explicit time/step budgets, per-tool
/// authorization, cancellation, persisted lifecycle snapshots, and audit events.
/// </summary>
public sealed class AgentTaskRunner
{
    private readonly IAgentTaskStore _taskStore;
    private readonly IAgentAuditStore _auditStore;
    private readonly IAgentPlanner _planner;
    private readonly IToolPolicy _toolPolicy;
    private readonly AgentToolRegistry _tools;

    public AgentTaskRunner(
        IAgentTaskStore taskStore,
        IAgentAuditStore auditStore,
        IAgentPlanner planner,
        IToolPolicy toolPolicy,
        AgentToolRegistry tools)
    {
        _taskStore = taskStore ?? throw new ArgumentNullException(nameof(taskStore));
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _toolPolicy = toolPolicy ?? throw new ArgumentNullException(nameof(toolPolicy));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
    }

    public async Task<AgentRunResult> RunAsync(
        string goal,
        string workspacePath,
        TimeSpan timeout,
        int maximumSteps,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        if (!Path.IsPathFullyQualified(workspacePath))
        {
            throw new ArgumentException(
                "Workspace path must be fully qualified.", nameof(workspacePath));
        }

        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout), "Timeout must be greater than zero and at most one hour.");
        }

        if (maximumSteps is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumSteps), "Maximum steps must be between 1 and 100.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var task = AgentTask.Create(goal);
        await _taskStore.SaveAsync(task, cancellationToken).ConfigureAwait(false);
        await RecordAsync(task, AgentAuditEventType.TaskCreated, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);
        var executionToken = linkedSource.Token;
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        var results = new List<ToolResult>();

        try
        {
            task = task.TransitionTo(AgentTaskStatus.Planning);
            await _taskStore.SaveAsync(task, executionToken).ConfigureAwait(false);
            await RecordAsync(task, AgentAuditEventType.PlanningStarted, cancellationToken: executionToken)
                .ConfigureAwait(false);

            var plan = await _planner.CreatePlanAsync(task, executionToken)
                .ConfigureAwait(false);

            if (plan?.Steps is null)
            {
                throw new InvalidOperationException("Planner returned no step collection.");
            }

            var steps = plan.Steps.ToArray();
            if (steps.Length > maximumSteps)
            {
                throw new InvalidOperationException(
                    $"Plan exceeds the configured limit of {maximumSteps} steps.");
            }

            await RecordAsync(
                task,
                AgentAuditEventType.PlanCreated,
                detail: $"step_count={steps.Length}",
                cancellationToken: executionToken).ConfigureAwait(false);

            task = task.TransitionTo(AgentTaskStatus.Running);
            await _taskStore.SaveAsync(task, executionToken).ConfigureAwait(false);
            await RecordAsync(task, AgentAuditEventType.ExecutionStarted, cancellationToken: executionToken)
                .ConfigureAwait(false);

            foreach (var step in steps)
            {
                executionToken.ThrowIfCancellationRequested();

                var request = step?.Request
                    ?? throw new InvalidOperationException("Plan contains an empty tool call.");

                if (!_tools.TryGet(request.ToolId, out var tool) || tool is null)
                {
                    throw new InvalidOperationException("Plan references an unregistered tool.");
                }

                var authorization = await _toolPolicy.AuthorizeAsync(
                    request, tool.Risk, executionToken).ConfigureAwait(false);

                if (!authorization.IsAllowed)
                {
                    await RecordAsync(
                        task,
                        AgentAuditEventType.ToolDenied,
                        tool.Id,
                        "policy_denied",
                        executionToken).ConfigureAwait(false);
                    throw new UnauthorizedAccessException("Tool execution was denied by policy.");
                }

                await RecordAsync(
                    task, AgentAuditEventType.ToolAuthorized, tool.Id,
                    "policy_allowed", executionToken).ConfigureAwait(false);
                await RecordAsync(
                    task, AgentAuditEventType.ToolStarted, tool.Id,
                    cancellationToken: executionToken).ConfigureAwait(false);

                var context = new ToolExecutionContext(task.Id, workspacePath, deadline);
                var result = await tool.ExecuteAsync(
                    request, context, executionToken).ConfigureAwait(false);

                results.Add(result);

                if (!result.Succeeded)
                {
                    await RecordAsync(
                        task,
                        AgentAuditEventType.ToolFailed,
                        tool.Id,
                        result.ErrorCode ?? "unspecified_error",
                        CancellationToken.None).ConfigureAwait(false);
                    throw new InvalidOperationException("Tool execution returned an unsuccessful result.");
                }

                await RecordAsync(
                    task, AgentAuditEventType.ToolSucceeded, tool.Id,
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
            }

            executionToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (executionToken.IsCancellationRequested)
        {
            var terminalStatus = cancellationToken.IsCancellationRequested
                ? AgentTaskStatus.Cancelled
                : AgentTaskStatus.TimedOut;

            task = task.TransitionTo(terminalStatus);
            await _taskStore.SaveAsync(task, CancellationToken.None).ConfigureAwait(false);
            await RecordAsync(
                task,
                terminalStatus == AgentTaskStatus.Cancelled
                    ? AgentAuditEventType.TaskCancelled
                    : AgentAuditEventType.TaskTimedOut,
                detail: terminalStatus.ToString(),
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
            return new AgentRunResult(task, results.AsReadOnly());
        }
        catch (Exception exception)
        {
            var failureCode = exception.GetType().Name;
            task = task.TransitionTo(
                AgentTaskStatus.Failed,
                failureReason: failureCode);
            await _taskStore.SaveAsync(task, CancellationToken.None).ConfigureAwait(false);
            await RecordAsync(
                task, AgentAuditEventType.TaskFailed, detail: failureCode,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
            return new AgentRunResult(task, results.AsReadOnly());
        }

        task = task.TransitionTo(AgentTaskStatus.Completed);
        await _taskStore.SaveAsync(task, CancellationToken.None).ConfigureAwait(false);
        await RecordAsync(
            task, AgentAuditEventType.TaskCompleted,
            cancellationToken: CancellationToken.None).ConfigureAwait(false);
        return new AgentRunResult(task, results.AsReadOnly());
    }

    private Task RecordAsync(
        AgentTask task,
        AgentAuditEventType eventType,
        string? toolId = null,
        string? detail = null,
        CancellationToken cancellationToken = default) =>
        _auditStore.AppendAsync(
            AgentAuditEvent.Create(task.Id, eventType, toolId, detail),
            cancellationToken);
}
