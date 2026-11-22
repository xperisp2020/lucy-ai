using Lucy.Core.Tools;

namespace Lucy.Core.Tests.Tools;

public sealed class WorkspaceFileReadToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReadsUtf8FileWithinConfiguredWorkspace()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "docs"));
            await File.WriteAllTextAsync(
                Path.Combine(directory, "docs", "readme.md"),
                "# LUCY\nCore workspace",
                new System.Text.UTF8Encoding(false));

            var tool = new WorkspaceFileReadTool(directory);
            var result = await tool.ExecuteAsync(
                Request("docs/readme.md"),
                Context(directory),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Contains("# LUCY", result.Output, StringComparison.Ordinal);
            Assert.Equal(ToolRisk.ReadOnly, tool.Risk);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("..\\outside.txt")]
    public async Task ExecuteAsync_RejectsParentTraversal(string path)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var tool = new WorkspaceFileReadTool(directory);

            var result = await tool.ExecuteAsync(
                Request(path),
                Context(directory),
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal("workspace_access_denied", result.ErrorCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RejectsAbsolutePathsAndMismatchedWorkspace()
    {
        var directory = CreateTemporaryDirectory();
        var otherDirectory = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "safe.txt"), "safe");
            var tool = new WorkspaceFileReadTool(directory);

            var absolute = await tool.ExecuteAsync(
                Request(Path.Combine(directory, "safe.txt")),
                Context(directory),
                CancellationToken.None);
            var mismatched = await tool.ExecuteAsync(
                Request("safe.txt"),
                Context(otherDirectory),
                CancellationToken.None);

            Assert.False(absolute.Succeeded);
            Assert.Equal("workspace_access_denied", absolute.ErrorCode);
            Assert.False(mismatched.Succeeded);
            Assert.Equal("workspace_access_denied", mismatched.ErrorCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            Directory.Delete(otherDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RejectsOversizedAndInvalidUtf8Files()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllBytesAsync(
                Path.Combine(directory, "large.bin"),
                new byte[WorkspaceFileReadTool.MaximumFileBytes + 1]);
            await File.WriteAllBytesAsync(
                Path.Combine(directory, "invalid.txt"),
                [0xC3, 0x28]);

            var tool = new WorkspaceFileReadTool(directory);
            var large = await tool.ExecuteAsync(
                Request("large.bin"), Context(directory), CancellationToken.None);
            var invalid = await tool.ExecuteAsync(
                Request("invalid.txt"), Context(directory), CancellationToken.None);

            Assert.False(large.Succeeded);
            Assert.Equal("file_too_large", large.ErrorCode);
            Assert.False(invalid.Succeeded);
            Assert.Equal("file_is_not_valid_utf8", invalid.ErrorCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnknownArguments()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var tool = new WorkspaceFileReadTool(directory);
            var request = new ToolRequest(
                tool.Id,
                new Dictionary<string, string>
                {
                    ["path"] = "a.txt",
                    ["encoding"] = "utf-8"
                });

            var result = await tool.ExecuteAsync(
                request, Context(directory), CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal("invalid_request", result.ErrorCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ToolRequest Request(string path) =>
        new(
            "workspace.file.read",
            new Dictionary<string, string> { ["path"] = path });

    private static ToolExecutionContext Context(string directory) =>
        new(Guid.NewGuid(), directory, DateTimeOffset.UtcNow.AddMinutes(1));

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lucy-workspace-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
