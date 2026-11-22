using System.Net;
using System.Text;
using System.Text.Json;
using Lucy.Core.Auditing;
using Lucy.Core.Memory;
using Lucy.Core.Policy;
using Lucy.Core.Providers;
using Lucy.Core.Runtime;
using Lucy.Core.Tasks;
using Lucy.Core.Tools;

namespace Lucy.Core.Tests.Runtime;

public sealed class LucyAgentRuntimeTests
{
    [Fact]
    public async Task Runtime_ComposesProviderPlannerMemoryAuditAndSafeWorkspaceTool()
    {
        var root = CreateTemporaryDirectory();
        var workspace = Path.Combine(root, "workspace");
        var state = Path.Combine(root, "state");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(state);
        await File.WriteAllTextAsync(
            Path.Combine(workspace, "README.md"),
            "LUCY runtime integration works.",
            new UTF8Encoding(false));

        try
        {
            var plan = """{"steps":[{"tool":"workspace.file.read","arguments":{"path":"README.md"}}]}""";
            var handler = new StubHandler((request, cancellationToken) =>
            {
                Assert.Equal("Bearer integration-secret", request.Headers.Authorization?.ToString());
                return Task.FromResult(JsonResponse(plan));
            });
            using var httpClient = new HttpClient(handler);

            var options = new LucyAgentOptions(
                Path.Combine(state, "lucy.db"),
                workspace,
                new Uri("https://provider.example/v1/"),
                "integration-model");

            var runtime = await LucyAgentRuntime.CreateAsync(
                options,
                httpClient,
                new StaticSecretProvider("integration-secret"));

            await runtime.Memory.SaveAsync(
                MemoryEntry.Create(
                    MemoryKind.Procedural,
                    "Read project documentation before making changes.",
                    "Verified engineering workflow"),
                CancellationToken.None);

            var result = await runtime.RunAsync(
                "Read README.md",
                TimeSpan.FromSeconds(10));

            Assert.Equal(AgentTaskStatus.Completed, result.Task.Status);
            Assert.Equal("LUCY runtime integration works.", Assert.Single(result.ToolResults).Output);

            var persistedTask = await runtime.Tasks.GetByIdAsync(
                result.Task.Id, CancellationToken.None);
            Assert.Equal(AgentTaskStatus.Completed, persistedTask!.Status);

            var events = await runtime.Audit.GetForTaskAsync(
                result.Task.Id, 100, CancellationToken.None);
            Assert.Equal(AgentAuditEventType.TaskCreated, events[0].EventType);
            Assert.Equal(AgentAuditEventType.TaskCompleted, events[^1].EventType);
            Assert.Contains(events, item => item.EventType == AgentAuditEventType.ToolSucceeded);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Runtime_FailsClosedWhenModelSelectsUnknownTool()
    {
        var root = CreateTemporaryDirectory();
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);

        try
        {
            var plan = """{"steps":[{"tool":"shell.execute","arguments":{"command":"whoami"}}]}""";
            using var httpClient = new HttpClient(new StubHandler((_, _) =>
                Task.FromResult(JsonResponse(plan))));

            var runtime = await LucyAgentRuntime.CreateAsync(
                new LucyAgentOptions(
                    Path.Combine(root, "lucy.db"),
                    workspace,
                    new Uri("https://provider.example/v1/"),
                    "integration-model"),
                httpClient,
                new StaticSecretProvider(null));

            var result = await runtime.RunAsync(
                "Run a shell command",
                TimeSpan.FromSeconds(10));

            Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
            Assert.Empty(result.ToolResults);
            Assert.Equal("InvalidDataException", result.Task.FailureReason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Options_RejectsDatabaseInsideReadableWorkspace()
    {
        var root = CreateTemporaryDirectory();
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);

        try
        {
            var options = new LucyAgentOptions(
                Path.Combine(workspace, "lucy.db"),
                workspace,
                new Uri("https://provider.example/v1/"),
                "model");

            Assert.Throws<ArgumentException>(options.Validate);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AllowListPolicy_EnforcesToolIdentityAndRiskCeiling()
    {
        var policy = new AllowListedToolPolicy(
            ["workspace.file.read"],
            ToolRisk.ReadOnly);

        var allowed = await policy.AuthorizeAsync(
            new ToolRequest("workspace.file.read", new Dictionary<string, string>()),
            ToolRisk.ReadOnly,
            CancellationToken.None);
        var wrongTool = await policy.AuthorizeAsync(
            new ToolRequest("network.fetch", new Dictionary<string, string>()),
            ToolRisk.ReadOnly,
            CancellationToken.None);
        var highRisk = await policy.AuthorizeAsync(
            new ToolRequest("workspace.file.read", new Dictionary<string, string>()),
            ToolRisk.NetworkAccess,
            CancellationToken.None);

        Assert.True(allowed.IsAllowed);
        Assert.False(wrongTool.IsAllowed);
        Assert.False(highRisk.IsAllowed);
    }

    private static HttpResponseMessage JsonResponse(string plan)
    {
        var json = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = plan } }
            }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lucy-runtime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StaticSecretProvider(string? secret) : ISecretProvider
    {
        public ValueTask<string?> GetSecretAsync(
            string secretName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(secret);
        }
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
