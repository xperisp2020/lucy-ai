using Lucy.Core.Execution;
using Lucy.Core.Memory;
using Lucy.Core.Planning;
using Lucy.Core.Providers;
using Lucy.Core.Tasks;
using Lucy.Core.Tools;

namespace Lucy.Core.Tests.Planning;

public sealed class JsonAgentPlannerTests
{
    [Fact]
    public async Task CreatePlanAsync_ParsesAllowlistedToolAndIncludesRelevantMemory()
    {
        var provider = new StubReasoningProvider(
            """{"steps":[{"tool":"file.read","arguments":{"path":"docs/README.md"}}]}""");
        var memory = new StubMemoryStore(
            MemoryEntry.Create(
                MemoryKind.Procedural,
                "Read repository documentation before editing.",
                "Verified engineering workflow"));
        var registry = new AgentToolRegistry([new StubTool("file.read", ToolRisk.ReadOnly)]);
        var definition = new AgentToolDefinition(
            "file.read",
            "Read a file inside the assigned workspace.",
            ToolRisk.ReadOnly,
            ["path"],
            ["path"]);
        var planner = new JsonAgentPlanner(provider, registry, [definition], memory);

        var plan = await planner.CreatePlanAsync(
            AgentTask.Create("Inspect repository documentation"),
            CancellationToken.None);

        var step = Assert.Single(plan.Steps);
        Assert.Equal("file.read", step.Request.ToolId);
        Assert.Equal("docs/README.md", step.Request.Arguments["path"]);
        Assert.Contains("Retrieved memory", provider.LastRequest!.Context!, StringComparison.Ordinal);
        Assert.Contains("untrusted reference data", provider.LastRequest.Context!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatePlanAsync_RejectsUnregisteredTool()
    {
        var planner = CreatePlanner(
            """{"steps":[{"tool":"shell.execute","arguments":{}}]}""");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            planner.CreatePlanAsync(AgentTask.Create("run a command"), CancellationToken.None));
    }

    [Fact]
    public async Task CreatePlanAsync_RejectsUnapprovedArguments()
    {
        var planner = CreatePlanner(
            """{"steps":[{"tool":"file.read","arguments":{"path":"a.txt","admin":"true"}}]}""");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            planner.CreatePlanAsync(AgentTask.Create("read a file"), CancellationToken.None));
    }

    [Fact]
    public async Task CreatePlanAsync_RejectsMissingRequiredArguments()
    {
        var planner = CreatePlanner(
            """{"steps":[{"tool":"file.read","arguments":{}}]}""");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            planner.CreatePlanAsync(AgentTask.Create("read a file"), CancellationToken.None));
    }

    [Fact]
    public async Task CreatePlanAsync_RejectsPlansAboveConfiguredStepLimit()
    {
        var planner = CreatePlanner(
            """{"steps":[{"tool":"file.read","arguments":{"path":"a"}},{"tool":"file.read","arguments":{"path":"b"}}]}""",
            maximumSteps: 1);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            planner.CreatePlanAsync(AgentTask.Create("read files"), CancellationToken.None));
    }

    [Fact]
    public async Task CreatePlanAsync_RejectsMalformedJson()
    {
        var planner = CreatePlanner("not json");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            planner.CreatePlanAsync(AgentTask.Create("inspect"), CancellationToken.None));
    }

    [Fact]
    public void Constructor_RejectsDefinitionThatDoesNotMatchRegisteredRisk()
    {
        var registry = new AgentToolRegistry([new StubTool("file.read", ToolRisk.ReadOnly)]);
        var definition = new AgentToolDefinition(
            "file.read", "Read a file.", ToolRisk.Destructive, ["path"]);

        Assert.Throws<ArgumentException>(() =>
        {
            _ = new JsonAgentPlanner(
                new StubReasoningProvider("""{"steps":[]}"""),
                registry,
                [definition]);
        });
    }

    private static JsonAgentPlanner CreatePlanner(
        string response,
        int maximumSteps = 20)
    {
        var registry = new AgentToolRegistry([new StubTool("file.read", ToolRisk.ReadOnly)]);
        var definition = new AgentToolDefinition(
            "file.read", "Read a file.", ToolRisk.ReadOnly, ["path"], ["path"]);

        return new JsonAgentPlanner(
            new StubReasoningProvider(response),
            registry,
            [definition],
            maximumSteps: maximumSteps);
    }

    private sealed class StubReasoningProvider(string content) : IReasoningProvider
    {
        public string ProviderId => "test-provider";
        public ReasoningRequest? LastRequest { get; private set; }

        public Task<ReasoningResponse> GenerateAsync(
            ReasoningRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(new ReasoningResponse(
                content, ProviderId, "test-model", TimeSpan.Zero));
        }
    }

    private sealed class StubTool(string id, ToolRisk risk) : IAgentTool
    {
        public string Id => id;
        public ToolRisk Risk => risk;

        public Task<ToolResult> ExecuteAsync(
            ToolRequest request,
            ToolExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ToolResult(true, "not executed by planner"));
    }

    private sealed class StubMemoryStore(MemoryEntry entry) : IMemoryStore
    {
        public Task SaveAsync(MemoryEntry memory, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<MemoryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<MemoryEntry?>(null);

        public Task<IReadOnlyList<MemoryEntry>> SearchAsync(
            string query,
            MemoryKind? kind,
            int maximumCount,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<MemoryEntry> entries = [entry];
            return Task.FromResult(entries);
        }

        public Task<IReadOnlyList<MemoryEntry>> GetAllAsync(
            int maximumCount,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<MemoryEntry> entries = [entry];
            return Task.FromResult(entries);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
