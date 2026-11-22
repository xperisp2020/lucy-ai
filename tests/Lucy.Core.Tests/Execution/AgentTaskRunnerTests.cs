using Lucy.Core.Auditing;
using Lucy.Core.Execution;
using Lucy.Core.Planning;
using Lucy.Core.Policy;
using Lucy.Core.Tasks;
using Lucy.Core.Tools;

namespace Lucy.Core.Tests.Execution;

public sealed class AgentTaskRunnerTests
{
    [Fact]
    public async Task RunAsync_ExecutesAuthorizedPlanAndPersistsLifecycle()
    {
        var store = new InMemoryTaskStore();
        var tool = new StubTool("echo", new ToolResult(true, "ok"));
        var runner = CreateRunner(store, new StubPlanner(
            new AgentPlan([new PlannedToolCall(
                new ToolRequest("echo", new Dictionary<string, string>()))])),
            new AllowAllPolicy(),
            [tool]);

        var result = await runner.RunAsync(
            "test goal", Path.GetFullPath("."), TimeSpan.FromSeconds(10), 3);

        Assert.Equal(AgentTaskStatus.Completed, result.Task.Status);
        Assert.Single(result.ToolResults);
        Assert.Equal(
            new[]
            {
                AgentTaskStatus.Pending,
                AgentTaskStatus.Planning,
                AgentTaskStatus.Running,
                AgentTaskStatus.Completed
            },
            store.Snapshots.Select(snapshot => snapshot.Status));
        Assert.Equal(1, tool.ExecutionCount);
    }

    [Fact]
    public async Task RunAsync_RecordsAuditEventsInExecutionOrder()
    {
        var taskStore = new InMemoryTaskStore();
        var auditStore = new InMemoryAuditStore();
        var tool = new StubTool("echo", new ToolResult(true, "ok"));
        var runner = new AgentTaskRunner(
            taskStore,
            auditStore,
            new StubPlanner(new AgentPlan([new PlannedToolCall(
                new ToolRequest("echo", new Dictionary<string, string>()))])),
            new AllowAllPolicy(),
            new AgentToolRegistry([tool]));

        var result = await runner.RunAsync(
            "audited goal", Path.GetFullPath("."), TimeSpan.FromSeconds(10), 2);
        var events = await auditStore.GetForTaskAsync(result.Task.Id, 100, CancellationToken.None);

        Assert.Equal(
            new[]
            {
                AgentAuditEventType.TaskCreated,
                AgentAuditEventType.PlanningStarted,
                AgentAuditEventType.PlanCreated,
                AgentAuditEventType.ExecutionStarted,
                AgentAuditEventType.ToolAuthorized,
                AgentAuditEventType.ToolStarted,
                AgentAuditEventType.ToolSucceeded,
                AgentAuditEventType.TaskCompleted
            },
            events.Select(item => item.EventType));
    }

    [Fact]
    public async Task RunAsync_DeniesToolBeforeExecution()
    {
        var store = new InMemoryTaskStore();
        var tool = new StubTool("danger", new ToolResult(true, "should not run"));
        var audit = new InMemoryAuditStore();
        var runner = new AgentTaskRunner(
            store, audit,
            new StubPlanner(new AgentPlan([new PlannedToolCall(
                new ToolRequest("danger", new Dictionary<string, string>()))])),
            new DenyByDefaultToolPolicy(),
            new AgentToolRegistry([tool]));

        var result = await runner.RunAsync(
            "denied goal", Path.GetFullPath("."), TimeSpan.FromSeconds(10), 3);

        Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
        Assert.Equal(0, tool.ExecutionCount);
        var events = await audit.GetForTaskAsync(result.Task.Id, 100, CancellationToken.None);
        Assert.Contains(events, item => item.EventType == AgentAuditEventType.ToolDenied);
        Assert.Equal(AgentAuditEventType.TaskFailed, events[^1].EventType);
    }

    [Fact]
    public async Task RunAsync_RejectsPlanOverStepBudgetBeforeExecuting()
    {
        var store = new InMemoryTaskStore();
        var tool = new StubTool("echo", new ToolResult(true, "ok"));
        var runner = CreateRunner(store, new StubPlanner(new AgentPlan(
        [
            new PlannedToolCall(new ToolRequest("echo", new Dictionary<string, string>())),
            new PlannedToolCall(new ToolRequest("echo", new Dictionary<string, string>()))
        ])), new AllowAllPolicy(), [tool]);

        var result = await runner.RunAsync(
            "bounded goal", Path.GetFullPath("."), TimeSpan.FromSeconds(10), 1);

        Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
        Assert.Equal(0, tool.ExecutionCount);
    }

    [Fact]
    public async Task RunAsync_ReportsTimeoutSeparatelyFromUserCancellation()
    {
        var store = new InMemoryTaskStore();
        var runner = CreateRunner(store, new BlockingPlanner(), new AllowAllPolicy(), []);

        var result = await runner.RunAsync(
            "timeout goal", Path.GetFullPath("."), TimeSpan.FromMilliseconds(50), 1);

        Assert.Equal(AgentTaskStatus.TimedOut, result.Task.Status);
    }

    [Fact]
    public async Task RunAsync_ReportsCallerCancellation()
    {
        var store = new InMemoryTaskStore();
        var runner = CreateRunner(store, new BlockingPlanner(), new AllowAllPolicy(), []);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var result = await runner.RunAsync(
            "cancel goal", Path.GetFullPath("."), TimeSpan.FromSeconds(5), 1,
            cancellation.Token);

        Assert.Equal(AgentTaskStatus.Cancelled, result.Task.Status);
    }

    private static AgentTaskRunner CreateRunner(
        IAgentTaskStore store,
        IAgentPlanner planner,
        IToolPolicy policy,
        IEnumerable<IAgentTool> tools) =>
        new(store, new InMemoryAuditStore(), planner, policy, new AgentToolRegistry(tools));

    private sealed class InMemoryTaskStore : IAgentTaskStore
    {
        public List<AgentTask> Snapshots { get; } = [];

        public Task SaveAsync(AgentTask task, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Snapshots.Add(task);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAuditStore : IAgentAuditStore
    {
        private readonly List<AgentAuditEvent> _events = [];

        public Task AppendAsync(AgentAuditEvent auditEvent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AgentAuditEvent>> GetForTaskAsync(
            Guid taskId,
            int maximumCount,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<AgentAuditEvent> result = _events
                .Where(item => item.TaskId == taskId)
                .Take(maximumCount)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    private sealed class StubPlanner(AgentPlan plan) : IAgentPlanner
    {
        public Task<AgentPlan> CreatePlanAsync(
            AgentTask task,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(plan);
        }
    }

    private sealed class BlockingPlanner : IAgentPlanner
    {
        public async Task<AgentPlan> CreatePlanAsync(
            AgentTask task,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new AgentPlan([]);
        }
    }

    private sealed class StubTool(string id, ToolResult result) : IAgentTool
    {
        public string Id => id;
        public ToolRisk Risk => ToolRisk.ReadOnly;
        public int ExecutionCount { get; private set; }

        public Task<ToolResult> ExecuteAsync(
            ToolRequest request,
            ToolExecutionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class AllowAllPolicy : IToolPolicy
    {
        public ValueTask<ToolAuthorization> AuthorizeAsync(
            ToolRequest request,
            ToolRisk risk,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ToolAuthorization.Allow("Test policy."));
        }
    }
}
