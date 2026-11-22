using Lucy.Core.Auditing;
using Lucy.Core.Execution;
using Lucy.Core.Memory;
using Lucy.Core.Planning;
using Lucy.Core.Policy;
using Lucy.Core.Providers;
using Lucy.Core.Storage;
using Lucy.Core.Tasks;
using Lucy.Core.Tools;

namespace Lucy.Core.Runtime;

/// <summary>
/// Composition root for the first usable LUCY runtime. It enables one explicit
/// read-only workspace tool; no shell, write, or network tool is registered.
/// </summary>
public sealed class LucyAgentRuntime
{
    private readonly AgentTaskRunner _taskRunner;
    private readonly string _workspacePath;
    private readonly int _maximumPlanSteps;

    private LucyAgentRuntime(
        AgentTaskRunner taskRunner,
        IAgentTaskRepository tasks,
        IAgentAuditStore audit,
        IMemoryStore memory,
        string workspacePath,
        int maximumPlanSteps)
    {
        _taskRunner = taskRunner;
        Tasks = tasks;
        Audit = audit;
        Memory = memory;
        _workspacePath = Path.GetFullPath(workspacePath);
        _maximumPlanSteps = maximumPlanSteps;
    }

    public IAgentTaskRepository Tasks { get; }
    public IAgentAuditStore Audit { get; }
    public IMemoryStore Memory { get; }

    public static async Task<LucyAgentRuntime> CreateAsync(
        LucyAgentOptions options,
        HttpClient httpClient,
        ISecretProvider secretProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(secretProvider);
        options.Validate();

        var tasks = await SqliteAgentTaskRepository.CreateAsync(
            options.DatabasePath, cancellationToken).ConfigureAwait(false);
        var audit = await SqliteAgentAuditStore.CreateAsync(
            options.DatabasePath, cancellationToken).ConfigureAwait(false);
        var memory = await SqliteMemoryStore.CreateAsync(
            options.DatabasePath, cancellationToken).ConfigureAwait(false);

        var fileReadTool = new WorkspaceFileReadTool(options.WorkspacePath);
        var registry = new AgentToolRegistry([fileReadTool]);
        var definition = new AgentToolDefinition(
            fileReadTool.Id,
            "Read a UTF-8 text file inside the configured workspace. Read-only; maximum 1 MiB.",
            fileReadTool.Risk,
            ["path"],
            ["path"]);

        var provider = new OpenAiCompatibleReasoningProvider(
            httpClient,
            new OpenAiCompatibleProviderOptions(
                options.ProviderEndpoint,
                options.Model,
                RequestTimeout: options.ProviderRequestTimeout),
            secretProvider);

        var planner = new JsonAgentPlanner(
            provider,
            registry,
            [definition],
            memory,
            options.MaximumPlanSteps);

        var policy = new AllowListedToolPolicy(
            [fileReadTool.Id],
            ToolRisk.ReadOnly);

        var runner = new AgentTaskRunner(
            tasks,
            audit,
            planner,
            policy,
            registry);

        return new LucyAgentRuntime(
            runner,
            tasks,
            audit,
            memory,
            options.WorkspacePath,
            options.MaximumPlanSteps);
    }

    public Task<AgentRunResult> RunAsync(
        string goal,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        _taskRunner.RunAsync(
            goal,
            _workspacePath,
            timeout,
            _maximumPlanSteps,
            cancellationToken);
}
