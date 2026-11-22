using System.Collections.Frozen;
using Lucy.Core.Tools;

namespace Lucy.Core.Planning;

/// <summary>
/// Trusted application-authored description of a tool. The model may select
/// only these identifiers and arguments; it cannot choose the tool's risk.
/// </summary>
public sealed class AgentToolDefinition
{
    public AgentToolDefinition(
        string id,
        string description,
        ToolRisk risk,
        IEnumerable<string> allowedArguments,
        IEnumerable<string>? requiredArguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(allowedArguments);

        if (id.Length > 128 || description.Length > 1000)
        {
            throw new ArgumentException("Tool definition exceeds supported limits.");
        }

        if (!Enum.IsDefined(risk))
        {
            throw new ArgumentOutOfRangeException(nameof(risk));
        }

        var allowed = allowedArguments.ToArray();
        var required = (requiredArguments ?? []).ToArray();

        if (allowed.Length > 20 || required.Length > 20 ||
            allowed.Any(string.IsNullOrWhiteSpace) ||
            required.Any(string.IsNullOrWhiteSpace) ||
            allowed.Distinct(StringComparer.Ordinal).Count() != allowed.Length ||
            required.Distinct(StringComparer.Ordinal).Count() != required.Length ||
            required.Except(allowed, StringComparer.Ordinal).Any())
        {
            throw new ArgumentException(
                "Tool arguments must be unique, bounded, and required arguments must be allowed.");
        }

        Id = id;
        Description = description;
        Risk = risk;
        AllowedArguments = allowed.ToFrozenSet(StringComparer.Ordinal);
        RequiredArguments = required.ToFrozenSet(StringComparer.Ordinal);
    }

    public string Id { get; }
    public string Description { get; }
    public ToolRisk Risk { get; }
    public IReadOnlySet<string> AllowedArguments { get; }
    public IReadOnlySet<string> RequiredArguments { get; }
}
