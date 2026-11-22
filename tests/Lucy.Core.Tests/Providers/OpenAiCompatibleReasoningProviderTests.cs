using System.Net;
using System.Text;
using System.Text.Json;
using Lucy.Core.Providers;

namespace Lucy.Core.Tests.Providers;

public sealed class OpenAiCompatibleReasoningProviderTests
{
    [Fact]
    public async Task GenerateAsync_SendsBoundedChatRequestAndUsesRuntimeSecret()
    {
        string? capturedAuthorization = null;
        string? capturedBody = null;
        Uri? capturedUri = null;
        HttpMethod? capturedMethod = null;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            capturedAuthorization = request.Headers.Authorization?.ToString();
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            capturedUri = request.RequestUri;
            capturedMethod = request.Method;
            return JsonResponse(
                """{"choices":[{"message":{"content":"A careful answer."}}]}""");
        });

        using var client = new HttpClient(handler);
        var provider = new OpenAiCompatibleReasoningProvider(
            client,
            new OpenAiCompatibleProviderOptions(
                new Uri("https://example.test/v1/"), "reasoner-model"),
            new StaticSecretProvider("runtime-secret"));

        var result = await provider.GenerateAsync(
            new ReasoningRequest(
                "Analyze this problem",
                ["Do not execute tools", "State uncertainty"],
                "Reference material"),
            CancellationToken.None);

        Assert.Equal("A careful answer.", result.Content);
        Assert.Equal("openai-compatible", result.Provider);
        Assert.Equal("reasoner-model", result.Model);
        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Equal(
            "https://example.test/v1/chat/completions",
            capturedUri!.ToString());
        Assert.Equal("Bearer runtime-secret", capturedAuthorization);

        using var payload = JsonDocument.Parse(capturedBody!);
        Assert.Equal("reasoner-model", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal(2, payload.RootElement.GetProperty("messages").GetArrayLength());
    }

    [Fact]
    public async Task GenerateAsync_DoesNotExposeProviderErrorBody()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("private-token-must-not-leak")
            }));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.GenerateAsync(
                new ReasoningRequest("goal", []),
                CancellationToken.None));

        Assert.DoesNotContain("private-token-must-not-leak", exception.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task GenerateAsync_RejectsOversizedResponse()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[2048])
            }));

        using var client = new HttpClient(handler);
        var provider = new OpenAiCompatibleReasoningProvider(
            client,
            new OpenAiCompatibleProviderOptions(
                new Uri("https://example.test/v1/"),
                "model",
                MaximumResponseBytes: 1024),
            new StaticSecretProvider(null));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            provider.GenerateAsync(
                new ReasoningRequest("goal", []),
                CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_EnforcesRequestTimeout()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse("""{"choices":[]}""");
        });

        using var client = new HttpClient(handler);
        var provider = new OpenAiCompatibleReasoningProvider(
            client,
            new OpenAiCompatibleProviderOptions(
                new Uri("https://example.test/v1/"),
                "model",
                RequestTimeout: TimeSpan.FromMilliseconds(50)),
            new StaticSecretProvider(null));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GenerateAsync(
                new ReasoningRequest("goal", []),
                CancellationToken.None));
    }

    [Fact]
    public void Options_RejectsInsecureRemoteHttpAndCredentialBearingUris()
    {
        var insecure = new OpenAiCompatibleProviderOptions(
            new Uri("http://remote.example/v1/"), "model");
        var credentialBearing = new OpenAiCompatibleProviderOptions(
            new Uri("https://user:password@example.test/v1/"), "model");

        Assert.Throws<ArgumentException>(insecure.Validate);
        Assert.Throws<ArgumentException>(credentialBearing.Validate);

        var local = new OpenAiCompatibleProviderOptions(
            new Uri("http://127.0.0.1:11434/v1/"), "local-model");
        local.Validate();
    }

    [Fact]
    public async Task EnvironmentSecretProvider_RestrictsVariableNames()
    {
        var provider = new EnvironmentSecretProvider();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await provider.GetSecretAsync("PATH", CancellationToken.None));
    }

    private static OpenAiCompatibleReasoningProvider CreateProvider(HttpClient client) =>
        new(
            client,
            new OpenAiCompatibleProviderOptions(
                new Uri("https://example.test/v1/"), "test-model"),
            new StaticSecretProvider(null));

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

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
