namespace PurgeKit.Core.Relocation;

/// <summary>Deletes folder trees permanently, including read-only entries, without following links.</summary>
public static class DirectoryDeletion
{
    /// <summary>
    /// Deletes <paramref name="path"/> and everything under it. Links inside the tree (or the path itself,
    /// if it is a link) are unlinked; their targets are never touched. Does nothing if the path does not exist.
    /// </summary>
    public static void DeleteTree(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var root = new DirectoryInfo(path);
        if (!root.Exists)
        {
            return;
        }
        if (IsLink(root))
        {
            root.Delete();
            return;
        }

        PrepareForDeletion(root);
        root.Delete(recursive: true);
    }

    // Recursive delete fails on read-only entries, and without elevation it also fails on junctions
    // (it tries to dismount them as volume mount points). Unlink and clear those first.
    private static void PrepareForDeletion(DirectoryInfo directory)
    {
        ClearReadOnly(directory);
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfo subdirectory && IsLink(subdirectory))
            {
                subdirectory.Delete();
            }
            else if (entry is DirectoryInfo regularDirectory)
            {
                PrepareForDeletion(regularDirectory);
            }
            else
            {
                ClearReadOnly(entry);
            }
        }
    }

    private static bool IsLink(FileSystemInfo entry) => entry.Attributes.HasFlag(FileAttributes.ReparsePoint);

    private static void ClearReadOnly(FileSystemInfo entry)
    {
        if (entry.Attributes.HasFlag(FileAttributes.ReadOnly))
        {
            entry.Attributes &= ~FileAttributes.ReadOnly;
        }
    }
}
