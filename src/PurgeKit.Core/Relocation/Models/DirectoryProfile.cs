namespace PurgeKit.Core.Relocation.Models;

/// <summary>Size and link inventory of a folder tree, gathered without following links.</summary>
/// <param name="SizeBytes">Total size of regular files.</param>
/// <param name="FileCount">Number of regular files.</param>
/// <param name="LinkPaths">Junctions, symlinks and other reparse points found in the tree.</param>
public sealed record DirectoryProfile(long SizeBytes, int FileCount, IReadOnlyList<string> LinkPaths);
