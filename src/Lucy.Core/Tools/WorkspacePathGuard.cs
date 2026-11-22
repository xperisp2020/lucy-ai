namespace Lucy.Core.Tools;

/// <summary>
/// Restricts file access to a configured workspace and rejects traversal and
/// reparse-point components. This reduces path escape risk but is not a
/// substitute for OS-level process isolation against concurrent local attackers.
/// </summary>
internal sealed class WorkspacePathGuard
{
    private readonly string _rootPath;
    private readonly string _rootWithSeparator;
    private readonly StringComparison _pathComparison;

    public WorkspacePathGuard(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _rootPath = Path.GetFullPath(workspaceRoot);

        if (!Directory.Exists(_rootPath))
        {
            throw new DirectoryNotFoundException("Configured workspace directory does not exist.");
        }

        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        _rootWithSeparator = Path.EndsInDirectorySeparator(_rootPath)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        EnsureNoReparsePoints(_rootPath);
    }

    public string RootPath => _rootPath;

    public string ResolveExistingFile(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (Path.IsPathRooted(relativePath) ||
            relativePath.Contains(':', StringComparison.Ordinal) ||
            relativePath.Any(char.IsControl))
        {
            throw new UnauthorizedAccessException("Only plain relative workspace paths are allowed.");
        }

        var normalized = relativePath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Replace('\', Path.DirectorySeparatorChar);

        var segments = normalized.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 ||
            segments.Any(segment => segment is "." or ".."))
        {
            throw new UnauthorizedAccessException("Workspace path contains an invalid segment.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        if (!fullPath.StartsWith(_rootWithSeparator, _pathComparison))
        {
            throw new UnauthorizedAccessException("Workspace path escapes the configured root.");
        }

        EnsureNoReparsePoints(fullPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Requested workspace file was not found.", relativePath);
        }

        return fullPath;
    }

    public void EnsureContextMatches(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        var fullPath = Path.GetFullPath(workspacePath);
        if (!string.Equals(fullPath, _rootPath, _pathComparison))
        {
            throw new UnauthorizedAccessException(
                "Tool execution workspace does not match the configured workspace root.");
        }
    }

    private static void EnsureNoReparsePoints(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath)
            ?? throw new UnauthorizedAccessException("Workspace path has no filesystem root.");
        var current = root;
        var remainder = fullPath[root.Length..];

        foreach (var segment in remainder.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                continue;
            }

            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException(
                    "Workspace paths cannot traverse symbolic links or reparse points.");
            }
        }
    }
}
