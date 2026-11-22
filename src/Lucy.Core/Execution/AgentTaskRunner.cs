using Lucy.Core.Planning;
using Lucy.Core.Policy;
using Lucy.Core.Tasks;
using Lucy.Core.Tools;

namespace Lucy.Core.Execution;

/// <summary>
/// Runs a planner-produced sequence with explicit time/step budgets, per-tool
/// authorization, cancellation, and persisted lifecycle snapshots.
/// </summary>
public sealed class AgentTaskRunner
{
    private readonly IAgentTaskStore _taskStore;
    private readonly IAgentPlanner _planner;
    private readonly IToolPolicy _toolPolicy;
    private readonly AgentToolRegistry _tools;

    public AgentTaskRunner(
        IAgentTaskStore taskStore,
        IAgentPlanner planner,
        IToolPolicy toolPolicy,
        AgentToolRegistry tools)
    {
        _taskStore = taskStore ?? throw new ArgumentNullException(nameof(taskStore));
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

            var plan = await _planner.CreatePlanAsync(task, executionToken)
                .ConfigureAwait(false);

            if (plan?.Steps is null)
            {
                throw new InvalidOperationException("Planner returned no step collection.");
            }

            if (plan.Steps.Count > maximumSteps)
            {
                throw new InvalidOperationException(
                    $"Plan contains {plan.Steps.Count} steps; limit is {maximumSteps}.");
            }

            task = task.TransitionTo(AgentTaskStatus.Running);
            await _taskStore.SaveAsync(task, executionToken).ConfigureAwait(false);

            foreach (var step in plan.Steps)
            {
                executionToken.ThrowIfCancellationRequested();

                var request = step?.Request
                    ?? throw new InvalidOperationException("Plan contains an empty tool call.");

                if (!_tools.TryGet(request.ToolId, out var tool) || tool is null)
                {
                    throw new InvalidOperationException(
                        $"Tool '{request.ToolId}' is not registered.");
                }

                var authorization = await _toolPolicy.AuthorizeAsync(
                    request, tool.Risk, executionToken).ConfigureAwait(false);

                if (!authorization.IsAllowed)
                {
                    throw new UnauthorizedAccessException(authorization.Reason);
                }

                var context = new ToolExecutionContext(task.Id, workspacePath, deadline);
                var result = await tool.ExecuteAsync(
                    request, context, executionToken).ConfigureAwait(false);

                results.Add(result);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Tool '{tool.Id}' failed: {result.ErrorCode ?? "unspecified error"}.");
                }
            }

            task = task.TransitionTo(AgentTaskStatus.Completed);
            await _taskStore.SaveAsync(task, CancellationToken.None).ConfigureAwait(false);
            return new AgentRunResult(task, results.AsReadOnly());
        }
        catch (OperationCanceledException) when (executionToken.IsCancellationRequested)
        {
            task = task.TransitionTo(AgentTaskStatus.Cancelled);
            await _taskStore.SaveAsync(task, CancellationToken.None).ConfigureAwait(false);
            return new AgentRunResult(task, results.AsReadOnly());
        }
        catch (Exception exception)
        {
            task = task.TransitionTo(
                AgentTaskStatus.Failed,
                failureReason: $"{exception.GetType().Name}: {exception.Message}");
            await _taskStore.SaveAsync(task, CancellationToken.None).ConfigureAwait(false);
            return new AgentRunResult(task, results.AsReadOnly());
        }
    }
}
