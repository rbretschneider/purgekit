namespace PurgeKit.Core.Relocation;

/// <summary>Copies folder trees with their metadata and verifies copies.</summary>
public interface IDirectoryCopier
{
    /// <summary>
    /// Copies <paramref name="sourcePath"/> to the new folder <paramref name="destinationPath"/>,
    /// preserving attributes, timestamps and access rules.
    /// </summary>
    /// <param name="bytesCopied">Receives the running total of bytes copied.</param>
    /// <exception cref="IOException"><paramref name="destinationPath"/> already exists.</exception>
    Task CopyAsync(string sourcePath, string destinationPath, IProgress<long>? bytesCopied, CancellationToken ct);

    /// <summary>Lists every difference between two trees; empty when the copy is faithful.</summary>
    Task<IReadOnlyList<string>> FindMismatchesAsync(string sourcePath, string destinationPath, CancellationToken ct);
}
