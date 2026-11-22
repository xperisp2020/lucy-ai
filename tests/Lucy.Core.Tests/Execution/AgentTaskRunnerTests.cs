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
    public async Task RunAsync_DeniesToolBeforeExecution()
    {
        var store = new InMemoryTaskStore();
        var tool = new StubTool("danger", new ToolResult(true, "should not run"));
        var runner = CreateRunner(store, new StubPlanner(
            new AgentPlan([new PlannedToolCall(
                new ToolRequest("danger", new Dictionary<string, string>()))])),
            new DenyByDefaultToolPolicy(),
            [tool]);

        var result = await runner.RunAsync(
            "denied goal", Path.GetFullPath("."), TimeSpan.FromSeconds(10), 3);

        Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
        Assert.Contains("UnauthorizedAccessException", result.Task.FailureReason);
        Assert.Equal(0, tool.ExecutionCount);
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

    private static AgentTaskRunner CreateRunner(
        IAgentTaskStore store,
        IAgentPlanner planner,
        IToolPolicy policy,
        IEnumerable<IAgentTool> tools) =>
        new(store, planner, policy, new AgentToolRegistry(tools));

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
