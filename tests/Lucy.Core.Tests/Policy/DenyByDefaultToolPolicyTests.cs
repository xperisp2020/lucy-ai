using Lucy.Core.Policy;
using Lucy.Core.Tools;

namespace Lucy.Core.Tests.Policy;

public sealed class DenyByDefaultToolPolicyTests
{
    [Fact]
    public async Task AuthorizeAsync_DeniesUnconfiguredTool()
    {
        var policy = new DenyByDefaultToolPolicy();
        var request = new ToolRequest(
            "shell.execute",
            new Dictionary<string, string>());

        var decision = await policy.AuthorizeAsync(
            request,
            ToolRisk.Destructive,
            CancellationToken.None);

        Assert.False(decision.IsAllowed);
        Assert.Contains("shell.execute", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeAsync_ObservesCancellation()
    {
        var policy = new DenyByDefaultToolPolicy();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await policy.AuthorizeAsync(
                new ToolRequest("test", new Dictionary<string, string>()),
                ToolRisk.ReadOnly,
                cancellation.Token));
    }
}
