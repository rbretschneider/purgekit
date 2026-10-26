namespace PurgeKit.Core.Relocation;

/// <summary>Pure path rules shared by every relocation component.</summary>
public static class RelocationPaths
{
    /// <summary>Suffix of the original folder while it is renamed aside during a swap.</summary>
    public const string PendingSuffix = ".purgekit-pending";

    /// <summary>Suffix of the copy being brought back during a restore.</summary>
    public const string RestoringSuffix = ".purgekit-restoring";

    /// <summary>Folder, relative to a drive root, that relocated folders go under by default.</summary>
    public const string DefaultFolderName = @"PurgeKit\Relocated";

    private const int LetteredRootLength = 3;

    /// <summary>Validates an absolute directory path and returns it without a trailing separator.</summary>
    /// <exception cref="ArgumentException">The path is empty or not absolute.</exception>
    public static string NormalizeDirectory(string path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A directory path is required.", paramName);
        }
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"'{path}' must be an absolute path.", paramName);
        }

        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        return full.Length > root.Length ? Path.TrimEndingDirectorySeparator(full) : root;
    }

    /// <summary>
    /// Maps a source folder to its place under <paramref name="targetRoot"/>, mirroring the drive letter
    /// and path so folders from different drives never collide:
    /// <c>C:\Program Files\Foo</c> becomes <c>{targetRoot}\C\Program Files\Foo</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The source is not on a lettered drive.</exception>
    public static string MapTarget(string sourcePath, string targetRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        var root = Path.GetPathRoot(sourcePath);
        if (!IsLetteredRoot(root))
        {
            throw new ArgumentException($"'{sourcePath}' is not on a lettered drive.", nameof(sourcePath));
        }

        return Path.Combine(targetRoot, root![..1].ToUpperInvariant(), sourcePath[root.Length..]);
    }

    /// <summary>Default target root on the given drive, for example <c>D:\PurgeKit\Relocated</c>.</summary>
    public static string DefaultTargetRoot(string driveRoot) => Path.Combine(driveRoot, DefaultFolderName);

    /// <summary>Where the original folder is parked while the junction is swapped in.</summary>
    public static string PendingPath(string sourcePath) => sourcePath + PendingSuffix;

    /// <summary>Where the restore copy is assembled before it replaces the junction.</summary>
    public static string RestoringPath(string sourcePath) => sourcePath + RestoringSuffix;

    /// <summary>True when <paramref name="path"/> is <paramref name="ancestor"/> or lies inside it.</summary>
    public static bool IsSameOrUnder(string path, string ancestor)
    {
        var trimmedPath = Path.TrimEndingDirectorySeparator(path);
        var trimmedAncestor = Path.TrimEndingDirectorySeparator(ancestor);
        return string.Equals(trimmedPath, trimmedAncestor, StringComparison.OrdinalIgnoreCase)
            || trimmedPath.StartsWith(trimmedAncestor + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when two directory paths name the same folder.</summary>
    public static bool AreSame(string? left, string? right) =>
        left is not null && right is not null
        && string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);

    private static bool IsLetteredRoot(string? root) =>
        root is { Length: LetteredRootLength } && char.IsAsciiLetter(root[0]) && root[1] == ':';
}
