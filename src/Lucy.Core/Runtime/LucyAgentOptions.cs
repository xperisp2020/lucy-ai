using Lucy.Core.Providers;

namespace Lucy.Core.Runtime;

/// <summary>
/// Explicit runtime configuration. Provider secrets are intentionally absent.
/// </summary>
public sealed record LucyAgentOptions(
    string DatabasePath,
    string WorkspacePath,
    Uri ProviderEndpoint,
    string Model,
    int MaximumPlanSteps = 10,
    TimeSpan? ProviderRequestTimeout = null)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkspacePath);
        ArgumentNullException.ThrowIfNull(ProviderEndpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(Model);

        var databasePath = Path.GetFullPath(DatabasePath);
        var workspacePath = Path.GetFullPath(WorkspacePath);
        if (!Path.IsPathFullyQualified(DatabasePath) ||
            !Path.IsPathFullyQualified(WorkspacePath))
        {
            throw new ArgumentException("Database and workspace paths must be absolute.");
        }

        if (!Directory.Exists(workspacePath))
        {
            throw new DirectoryNotFoundException("Configured workspace directory does not exist.");
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var workspacePrefix = Path.EndsInDirectorySeparator(workspacePath)
            ? workspacePath
            : workspacePath + Path.DirectorySeparatorChar;

        if (databasePath.StartsWith(workspacePrefix, comparison))
        {
            throw new ArgumentException(
                "The SQLite database must be outside the readable workspace.",
                nameof(DatabasePath));
        }

        if (MaximumPlanSteps is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPlanSteps), "Maximum plan steps must be between 1 and 100.");
        }

        new OpenAiCompatibleProviderOptions(
            ProviderEndpoint,
            Model,
            RequestTimeout: ProviderRequestTimeout).Validate();
    }
}
