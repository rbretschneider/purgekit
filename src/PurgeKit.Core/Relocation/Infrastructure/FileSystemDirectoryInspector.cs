using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Infrastructure;

/// <summary>Walks a folder tree on disk, recording links instead of following them.</summary>
public sealed class FileSystemDirectoryInspector : IDirectoryInspector
{
    /// <inheritdoc />
    public DirectoryProfile Inspect(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var root = new DirectoryInfo(path);
        if (!root.Exists)
        {
            throw new DirectoryNotFoundException($"'{path}' does not exist.");
        }

        var tally = new Tally();
        Walk(root, tally, ct);
        return new DirectoryProfile(tally.Bytes, tally.Files, tally.Links);
    }

    private static void Walk(DirectoryInfo directory, Tally tally, CancellationToken ct)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            ct.ThrowIfCancellationRequested();
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                tally.Links.Add(entry.FullName);
            }
            else if (entry is FileInfo file)
            {
                tally.Bytes += file.Length;
                tally.Files++;
            }
            else
            {
                Walk((DirectoryInfo)entry, tally, ct);
            }
        }
    }

    private sealed class Tally
    {
        public long Bytes { get; set; }

        public int Files { get; set; }

        public List<string> Links { get; } = [];
    }
}
