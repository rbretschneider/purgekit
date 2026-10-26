using PurgeKit.Core.Models;
using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Rules;

/// <summary>Requires a different volume with room for the folder plus headroom.</summary>
public sealed class CapacityRule : IEligibilityRule
{
    private readonly IVolumeInfoProvider _volumes;

    /// <summary>Creates the rule.</summary>
    public CapacityRule(IVolumeInfoProvider volumes)
    {
        _volumes = volumes ?? throw new ArgumentNullException(nameof(volumes));
    }

    /// <inheritdoc />
    public IReadOnlyList<RelocationBlocker> Evaluate(EligibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var source = _volumes.GetVolume(context.Request.SourcePath);
        var target = _volumes.GetVolume(context.Request.TargetRoot);
        if (source is null || target is null)
        {
            // A missing target is reported by TargetVolumeRule; a missing source by the checker.
            return [];
        }
        if (RelocationPaths.AreSame(source.RootPath, target.RootPath))
        {
            return [new RelocationBlocker(BlockerKind.SameVolume,
                $"The folder is already on {target.RootPath}. Pick a different drive to free up space.")];
        }
        return IsTooSmall(target, context.Profile.SizeBytes)
            ? [new RelocationBlocker(BlockerKind.InsufficientSpace,
                $"{target.RootPath} has {ScanResult.FormatSize(target.AvailableFreeBytes)} free; "
                + $"this needs {ScanResult.FormatSize(RelocationCapacity.RequiredFreeBytes(context.Profile.SizeBytes))}.")]
            : [];
    }

    private static bool IsTooSmall(VolumeDetails target, long sizeBytes) =>
        target.AvailableFreeBytes < RelocationCapacity.RequiredFreeBytes(sizeBytes);
}
