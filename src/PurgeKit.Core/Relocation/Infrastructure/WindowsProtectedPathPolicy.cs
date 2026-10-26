namespace PurgeKit.Core.Relocation.Infrastructure;

/// <summary>
/// Protects Windows, shared component folders, profile and Program Files roots, and PurgeKit's own
/// folder. Ordinary app folders under Program Files are allowed but need elevation.
/// </summary>
public sealed class WindowsProtectedPathPolicy : IProtectedPathPolicy
{
    private readonly string[] _immovableFolders;
    private readonly string[] _immovableTrees;
    private readonly string[] _elevatedTrees;

    /// <summary>Creates the policy from the current machine's special folders.</summary>
    public WindowsProtectedPathPolicy()
    {
        var programFiles = Folder(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Folder(Environment.SpecialFolder.ProgramFilesX86);
        var programData = Folder(Environment.SpecialFolder.CommonApplicationData);
        var windows = Folder(Environment.SpecialFolder.Windows);
        var profile = Folder(Environment.SpecialFolder.UserProfile);

        _immovableFolders = NonEmpty(
            programFiles, programFilesX86, programData, Path.GetDirectoryName(profile), profile,
            Folder(Environment.SpecialFolder.ApplicationData), Folder(Environment.SpecialFolder.LocalApplicationData),
            Path.Combine(profile, "AppData"), Path.Combine(profile, "AppData", "LocalLow"));
        _immovableTrees = NonEmpty(
            windows, Path.Combine(programFiles, "WindowsApps"), Path.Combine(programFiles, "Common Files"),
            CombineIfSet(programFilesX86, "Common Files"), Path.Combine(programData, "Microsoft"),
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        _elevatedTrees = NonEmpty(programFiles, programFilesX86, programData, windows);
    }

    /// <inheritdoc />
    public bool IsProtected(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return IsDriveRoot(path)
            || _immovableFolders.Any(folder => RelocationPaths.AreSame(path, folder))
            || _immovableTrees.Any(tree => OverlapsTree(path, tree));
    }

    /// <inheritdoc />
    public bool RequiresElevation(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return _elevatedTrees.Any(tree => RelocationPaths.IsSameOrUnder(path, tree));
    }

    // Moving a folder that contains a protected tree is as bad as moving the tree itself.
    private static bool OverlapsTree(string path, string tree) =>
        RelocationPaths.IsSameOrUnder(path, tree) || RelocationPaths.IsSameOrUnder(tree, path);

    private static bool IsDriveRoot(string path) =>
        RelocationPaths.AreSame(Path.GetPathRoot(path), path);

    private static string Folder(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);

    private static string? CombineIfSet(string root, string child) =>
        string.IsNullOrEmpty(root) ? null : Path.Combine(root, child);

    private static string[] NonEmpty(params string?[] paths) =>
        paths.Where(p => !string.IsNullOrEmpty(p)).Select(p => p!).ToArray();
}
