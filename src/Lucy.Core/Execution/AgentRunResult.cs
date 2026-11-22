using Lucy.Core.Tasks;
using Lucy.Core.Tools;

namespace Lucy.Core.Execution;

public sealed record AgentRunResult(
    AgentTask Task,
    IReadOnlyList<ToolResult> ToolResults);
