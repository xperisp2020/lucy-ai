namespace Lucy.Core.Tools;

public enum ToolRisk
{
    ReadOnly,
    WorkspaceWrite,
    NetworkAccess,
    Destructive,
    ExternalSideEffect
}

public sealed record ToolRequest(
    string ToolId,
    IReadOnlyDictionary<string, string> Arguments);

public sealed record ToolResult(
    bool Succeeded,
    string Output,
    string? ErrorCode = null);

public sealed record ToolExecutionContext(
    Guid TaskId,
    string WorkspacePath,
    DateTimeOffset Deadline);

public interface IAgentTool
{
    string Id { get; }
    ToolRisk Risk { get; }

    Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken);
}
