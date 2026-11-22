namespace Lucy.Core.Providers;

/// <summary>
/// Retrieves secrets at runtime. Implementations must never log returned values.
/// </summary>
public interface ISecretProvider
{
    ValueTask<string?> GetSecretAsync(
        string secretName,
        CancellationToken cancellationToken);
}
