namespace Lucy.Core.Providers;

public sealed record OpenAiCompatibleProviderOptions(
    Uri Endpoint,
    string Model,
    string ApiKeyEnvironmentVariableName = "LUCY_LLM_API_KEY",
    TimeSpan? RequestTimeout = null,
    int MaximumResponseBytes = 1_048_576)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(Model);
        ArgumentException.ThrowIfNullOrWhiteSpace(ApiKeyEnvironmentVariableName);

        if (Model.Length > 128)
        {
            throw new ArgumentException("Model identifier cannot exceed 128 characters.", nameof(Model));
        }

        if (!ApiKeyEnvironmentVariableName.StartsWith("LUCY_", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Secret environment variable names must use the LUCY_ prefix.",
                nameof(ApiKeyEnvironmentVariableName));
        }

        var isLocalHttp = Endpoint.IsLoopback;
        if (Endpoint.Scheme != Uri.UriSchemeHttps &&
            !(Endpoint.Scheme == Uri.UriSchemeHttp && isLocalHttp))
        {
            throw new ArgumentException(
                "Remote provider endpoints must use HTTPS; HTTP is allowed only for loopback endpoints.",
                nameof(Endpoint));
        }

        if (!string.IsNullOrEmpty(Endpoint.UserInfo) ||
            !string.IsNullOrEmpty(Endpoint.Query) ||
            !string.IsNullOrEmpty(Endpoint.Fragment) ||
            !Endpoint.AbsolutePath.EndsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Endpoint must be a credential-free base URI ending in '/'.",
                nameof(Endpoint));
        }

        var timeout = RequestTimeout ?? TimeSpan.FromSeconds(60);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(
                nameof(RequestTimeout), "Request timeout must be between 0 and 5 minutes.");
        }

        if (MaximumResponseBytes is < 1024 or > 4_194_304)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumResponseBytes), "Maximum response size must be between 1 KiB and 4 MiB.");
        }
    }
}
