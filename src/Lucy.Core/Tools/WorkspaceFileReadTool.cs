using System.Text;

namespace Lucy.Core.Tools;

/// <summary>
/// Reads bounded UTF-8 text files from one configured workspace only.
/// No shell, network, write, or arbitrary-path capability is exposed.
/// </summary>
public sealed class WorkspaceFileReadTool : IAgentTool
{
    public const int MaximumFileBytes = 1_048_576;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly WorkspacePathGuard _pathGuard;

    public WorkspaceFileReadTool(string workspaceRoot) =>
        _pathGuard = new WorkspacePathGuard(workspaceRoot);

    public string Id => "workspace.file.read";
    public ToolRisk Risk => ToolRisk.ReadOnly;

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(request.ToolId, Id, StringComparison.Ordinal) ||
            request.Arguments is null ||
            request.Arguments.Count != 1 ||
            !request.Arguments.TryGetValue("path", out var relativePath))
        {
            return new ToolResult(false, string.Empty, "invalid_request");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pathGuard.EnsureContextMatches(context.WorkspacePath);
            var fullPath = _pathGuard.ResolveExistingFile(relativePath);

            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            if (stream.Length > MaximumFileBytes)
            {
                return new ToolResult(false, string.Empty, "file_too_large");
            }

            using var output = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (output.Length + read > MaximumFileBytes)
                {
                    return new ToolResult(false, string.Empty, "file_too_large");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }

            var content = StrictUtf8.GetString(output.ToArray());
            return new ToolResult(true, content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DecoderFallbackException)
        {
            return new ToolResult(false, string.Empty, "file_is_not_valid_utf8");
        }
        catch (UnauthorizedAccessException)
        {
            return new ToolResult(false, string.Empty, "workspace_access_denied");
        }
        catch (FileNotFoundException)
        {
            return new ToolResult(false, string.Empty, "file_not_found");
        }
        catch (DirectoryNotFoundException)
        {
            return new ToolResult(false, string.Empty, "directory_not_found");
        }
        catch (IOException)
        {
            return new ToolResult(false, string.Empty, "file_read_failed");
        }
    }
}
