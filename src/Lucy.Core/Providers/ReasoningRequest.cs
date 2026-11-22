namespace Lucy.Core.Providers;

public sealed record ReasoningRequest(
    string Goal,
    IReadOnlyList<string> Constraints,
    string? Context = null);

public sealed record ReasoningResponse(
    string Content,
    string Provider,
    string Model,
    TimeSpan Duration);
