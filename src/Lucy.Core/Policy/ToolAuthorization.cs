using Lucy.Core.Tools;

namespace Lucy.Core.Policy;

public sealed record ToolAuthorization(
    bool IsAllowed,
    string Reason)
{
    public static ToolAuthorization Allow(string reason) => new(true, reason);
    public static ToolAuthorization Deny(string reason) => new(false, reason);
}

public interface IToolPolicy
{
    ValueTask<ToolAuthorization> AuthorizeAsync(
        ToolRequest request,
        ToolRisk risk,
        CancellationToken cancellationToken);
}

/// <summary>
/// Secure baseline: no tool is authorized until an explicit policy is supplied.
/// </summary>
public sealed class DenyByDefaultToolPolicy : IToolPolicy
{
    public ValueTask<ToolAuthorization> AuthorizeAsync(
        ToolRequest request,
        ToolRisk risk,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(
            ToolAuthorization.Deny($"No policy rule authorizes tool '{request.ToolId}'."));
    }
}
