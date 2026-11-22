using System.Collections.Frozen;
using Lucy.Core.Tools;

namespace Lucy.Core.Policy;

/// <summary>
/// Allows only explicitly named tools at or below a configured risk ceiling.
/// Unknown tools and higher-risk operations are denied.
/// </summary>
public sealed class AllowListedToolPolicy : IToolPolicy
{
    private readonly FrozenSet<string> _allowedToolIds;
    private readonly ToolRisk _maximumAllowedRisk;

    public AllowListedToolPolicy(
        IEnumerable<string> allowedToolIds,
        ToolRisk maximumAllowedRisk = ToolRisk.ReadOnly)
    {
        ArgumentNullException.ThrowIfNull(allowedToolIds);
        if (!Enum.IsDefined(maximumAllowedRisk))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAllowedRisk));
        }

        var identifiers = allowedToolIds.ToArray();
        if (identifiers.Length > 100 ||
            identifiers.Any(string.IsNullOrWhiteSpace) ||
            identifiers.Any(identifier => identifier.Length > 128) ||
            identifiers.Distinct(StringComparer.Ordinal).Count() != identifiers.Length)
        {
            throw new ArgumentException(
                "Tool allowlist identifiers must be unique, non-empty, and bounded.",
                nameof(allowedToolIds));
        }

        _allowedToolIds = identifiers.ToFrozenSet(StringComparer.Ordinal);
        _maximumAllowedRisk = maximumAllowedRisk;
    }

    public ValueTask<ToolAuthorization> AuthorizeAsync(
        ToolRequest request,
        ToolRisk risk,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Enum.IsDefined(risk) ||
            risk > _maximumAllowedRisk ||
            !_allowedToolIds.Contains(request.ToolId))
        {
            return ValueTask.FromResult(
                ToolAuthorization.Deny("Tool is not covered by the configured allowlist and risk ceiling."));
        }

        return ValueTask.FromResult(
            ToolAuthorization.Allow("Tool is explicitly allowlisted within the configured risk ceiling."));
    }
}
