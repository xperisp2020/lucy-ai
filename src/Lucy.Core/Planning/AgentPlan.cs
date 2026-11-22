using Lucy.Core.Tools;

namespace Lucy.Core.Planning;

/// <summary>
/// A bounded, ordered plan. The executor validates its size and authorizes each
/// tool call independently; a plan is never permission to execute by itself.
/// </summary>
public sealed record AgentPlan(IReadOnlyList<PlannedToolCall> Steps);

public sealed record PlannedToolCall(ToolRequest Request);
