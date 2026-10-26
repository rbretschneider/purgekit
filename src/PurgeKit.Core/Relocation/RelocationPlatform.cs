namespace PurgeKit.Core.Relocation;

/// <summary>Every OS-facing dependency of the relocation module, swappable as a set (e.g. for tests).</summary>
public sealed record RelocationPlatform
{
    /// <summary>Durable relocation journal.</summary>
    public required IRelocationJournal Journal { get; init; }

    /// <summary>Junction operations.</summary>
    public required IJunctionManager Junctions { get; init; }

    /// <summary>Folder copy and verification.</summary>
    public required IDirectoryCopier Copier { get; init; }

    /// <summary>Folder size and link inventory.</summary>
    public required IDirectoryInspector Inspector { get; init; }

    /// <summary>Volume lookups.</summary>
    public required IVolumeInfoProvider Volumes { get; init; }

    /// <summary>Open-file detection.</summary>
    public required IFileLockDetector Locks { get; init; }

    /// <summary>Service and driver lookups.</summary>
    public required IServiceRegistry Services { get; init; }

    /// <summary>Elevation status.</summary>
    public required IElevationStatus Elevation { get; init; }

    /// <summary>Protected-folder policy.</summary>
    public required IProtectedPathPolicy Policy { get; init; }

    /// <summary>Clock used for journal timestamps.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}
