namespace PurgeKit.Core.Relocation.Apps.Infrastructure;

/// <summary>Sums file sizes, skipping links and unreadable entries. Good enough to rank apps by size.</summary>
public sealed class FolderSizeEstimator : IFolderSizeEstimator
{
    private static readonly EnumerationOptions ReadableTreeWithoutLinks = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = true
    };

    /// <inheritdoc />
    public long EstimateBytes(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = new DirectoryInfo(path);
        if (!directory.Exists)
        {
            return 0;
        }

        long total = 0;
        foreach (var file in directory.EnumerateFiles("*", ReadableTreeWithoutLinks))
        {
            ct.ThrowIfCancellationRequested();
            total += file.Length;
        }
        return total;
    }
}
