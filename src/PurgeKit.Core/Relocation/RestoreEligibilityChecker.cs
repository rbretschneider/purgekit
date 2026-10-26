using PurgeKit.Core.Models;
using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>Checks that a relocated folder can be moved back to where it came from.</summary>
public sealed class RestoreEligibilityChecker : IRestoreEligibilityChecker
{
    private readonly IFileLockDetector _locks;
    private readonly IVolumeInfoProvider _volumes;
    private readonly IJunctionManager _junctions;

    /// <summary>Creates the checker.</summary>
    public RestoreEligibilityChecker(IFileLockDetector locks, IVolumeInfoProvider volumes, IJunctionManager junctions)
    {
        _locks = locks ?? throw new ArgumentNullException(nameof(locks));
        _volumes = volumes ?? throw new ArgumentNullException(nameof(volumes));
        _junctions = junctions ?? throw new ArgumentNullException(nameof(junctions));
    }

    /// <inheritdoc />
    public Task<EligibilityReport> CheckAsync(RelocationEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Task.Run(() => Evaluate(entry), ct);
    }

    private EligibilityReport Evaluate(RelocationEntry entry)
    {
        IEnumerable<RelocationBlocker> blockers = [.. JunctionBlockers(entry), .. LockBlockers(entry), .. SpaceBlockers(entry)];
        return new EligibilityReport(entry.TargetPath, entry.SourcePath, entry.SizeBytes, blockers.ToList());
    }

    private IEnumerable<RelocationBlocker> JunctionBlockers(RelocationEntry entry)
    {
        if (!RelocationPaths.AreSame(_junctions.GetTarget(entry.SourcePath), entry.TargetPath))
        {
            yield return new RelocationBlocker(BlockerKind.JunctionMissing,
                $"{entry.SourcePath} no longer points to {entry.TargetPath}. Something changed it outside PurgeKit.");
        }
    }

    private IEnumerable<RelocationBlocker> LockBlockers(RelocationEntry entry)
    {
        var apps = _locks.FindLockingProcesses(entry.TargetPath);
        if (apps.Count > 0)
        {
            yield return new RelocationBlocker(BlockerKind.InUse, $"Close {string.Join(", ", apps)} first.");
        }
    }

    private IEnumerable<RelocationBlocker> SpaceBlockers(RelocationEntry entry)
    {
        var volume = _volumes.GetVolume(entry.SourcePath);
        var required = RelocationCapacity.RequiredFreeBytes(entry.SizeBytes);
        if (volume is null || volume.AvailableFreeBytes < required)
        {
            yield return new RelocationBlocker(BlockerKind.InsufficientSpace,
                $"Moving it back needs {ScanResult.FormatSize(required)} free on the original drive.");
        }
    }
}
