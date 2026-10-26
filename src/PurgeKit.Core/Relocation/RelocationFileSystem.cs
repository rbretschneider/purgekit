namespace PurgeKit.Core.Relocation;

/// <summary>The filesystem operations a relocation performs.</summary>
/// <param name="Copier">Copies and verifies folder trees.</param>
/// <param name="Junctions">Creates and removes junctions.</param>
public sealed record RelocationFileSystem(IDirectoryCopier Copier, IJunctionManager Junctions);
