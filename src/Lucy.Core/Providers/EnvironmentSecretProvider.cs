namespace Lucy.Core.Providers;

/// <summary>
/// Minimal environment-backed secret source for local development.
/// Production deployments should prefer an OS or managed secret store.
/// </summary>
public sealed class EnvironmentSecretProvider : ISecretProvider
{
    public ValueTask<string?> GetSecretAsync(
        string secretName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        if (!secretName.StartsWith("LUCY_", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Only environment variables prefixed with LUCY_ may be read as secrets.",
                nameof(secretName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Environment.GetEnvironmentVariable(secretName));
    }
}
