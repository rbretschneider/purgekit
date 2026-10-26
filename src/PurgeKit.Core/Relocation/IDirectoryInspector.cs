using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>Measures a folder tree.</summary>
public interface IDirectoryInspector
{
    /// <summary>Size, file count and links of <paramref name="path"/>, without following links.</summary>
    /// <exception cref="UnauthorizedAccessException">Part of the tree cannot be read.</exception>
    DirectoryProfile Inspect(string path, CancellationToken ct);
}
