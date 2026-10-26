namespace PurgeKit.Core.Relocation.Models;

/// <summary>
/// A request to move <see cref="SourcePath"/> under <see cref="TargetRoot"/> on another drive.
/// Both paths are validated and normalized on construction.
/// </summary>
public sealed record RelocationRequest
{
    /// <summary>Creates a validated request.</summary>
    /// <param name="sourcePath">Absolute path of the folder to move.</param>
    /// <param name="targetRoot">Absolute folder on the target drive that relocated folders are placed under.</param>
    /// <exception cref="ArgumentException">Either path is empty or not absolute.</exception>
    public RelocationRequest(string sourcePath, string targetRoot)
    {
        SourcePath = RelocationPaths.NormalizeDirectory(sourcePath, nameof(sourcePath));
        TargetRoot = RelocationPaths.NormalizeDirectory(targetRoot, nameof(targetRoot));
    }

    /// <summary>Absolute, normalized path of the folder to move.</summary>
    public string SourcePath { get; }

    /// <summary>Absolute, normalized folder that relocated folders are placed under.</summary>
    public string TargetRoot { get; }
}
