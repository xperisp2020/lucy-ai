using Lucy.Core.Tools;

namespace Lucy.Core.Execution;

/// <summary>
/// Explicit registry. Unknown tools are rejected; duplicate identifiers fail
/// during composition rather than producing order-dependent behavior.
/// </summary>
public sealed class AgentToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAgentTool> _tools;

    public AgentToolRegistry(IEnumerable<IAgentTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var registry = new Dictionary<string, IAgentTool>(StringComparer.Ordinal);
        foreach (var tool in tools)
        {
            ArgumentNullException.ThrowIfNull(tool);
            ArgumentException.ThrowIfNullOrWhiteSpace(tool.Id);

            if (!registry.TryAdd(tool.Id, tool))
            {
                throw new ArgumentException(
                    $"Duplicate tool identifier '{tool.Id}'.", nameof(tools));
            }
        }

        _tools = registry;
    }

    public bool TryGet(string toolId, out IAgentTool? tool) =>
        _tools.TryGetValue(toolId, out tool);
}
