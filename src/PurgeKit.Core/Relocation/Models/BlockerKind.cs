namespace PurgeKit.Core.Relocation.Models;

/// <summary>Reasons a folder cannot be relocated (or restored) right now.</summary>
public enum BlockerKind
{
    /// <summary>The folder does not exist.</summary>
    SourceNotFound,

    /// <summary>Windows, shared system folders, profile roots, or PurgeKit itself.</summary>
    ProtectedPath,

    /// <summary>The folder is already a link or already in the relocation journal.</summary>
    AlreadyRelocated,

    /// <summary>The folder contains junctions, symlinks or other reparse points.</summary>
    ContainsLinks,

    /// <summary>Part of the folder could not be read.</summary>
    Inaccessible,

    /// <summary>A running process has files in the folder open.</summary>
    InUse,

    /// <summary>A Windows service or driver is installed from the folder.</summary>
    ServiceRegistered,

    /// <summary>The folder can only be changed by an elevated process.</summary>
    RequiresElevation,

    /// <summary>Source and target are on the same volume, so nothing would be freed.</summary>
    SameVolume,

    /// <summary>The target drive is missing or not ready.</summary>
    TargetUnavailable,

    /// <summary>The target is removable or network storage, which can disappear.</summary>
    TargetNotFixedDrive,

    /// <summary>The target file system cannot hold junction targets (not NTFS or ReFS).</summary>
    TargetFileSystemUnsupported,

    /// <summary>Not enough free space on the receiving drive.</summary>
    InsufficientSpace,

    /// <summary>Something already exists at the computed target path.</summary>
    TargetAlreadyExists,

    /// <summary>Leftovers of an earlier interrupted relocation sit next to the folder.</summary>
    LeftoverArtifacts,

    /// <summary>The target root is inside the folder being moved.</summary>
    TargetInsideSource,

    /// <summary>The original path is no longer a junction to the relocated folder.</summary>
    JunctionMissing
}
