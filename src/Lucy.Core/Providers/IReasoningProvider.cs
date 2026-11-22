namespace Lucy.Core.Providers;

/// <summary>
/// Vendor-neutral boundary for reasoning providers. Implementations must honor
/// cancellation and must not execute tools as a side effect.
/// </summary>
public interface IReasoningProvider
{
    string ProviderId { get; }

    Task<ReasoningResponse> GenerateAsync(
        ReasoningRequest request,
        CancellationToken cancellationToken);
}
