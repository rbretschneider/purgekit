using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Rules;

/// <summary>
/// Requires the target to be a present, fixed NTFS or ReFS drive. Junctions cannot point at other
/// file systems, and a removable or network drive could vanish and break the app.
/// </summary>
public sealed class TargetVolumeRule : IEligibilityRule
{
    private static readonly string[] SupportedFileSystems = ["NTFS", "ReFS"];

    private readonly IVolumeInfoProvider _volumes;

    /// <summary>Creates the rule.</summary>
    public TargetVolumeRule(IVolumeInfoProvider volumes)
    {
        _volumes = volumes ?? throw new ArgumentNullException(nameof(volumes));
    }

    /// <inheritdoc />
    public IReadOnlyList<RelocationBlocker> Evaluate(EligibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var volume = _volumes.GetVolume(context.Request.TargetRoot);
        if (volume is null)
        {
            return [new RelocationBlocker(BlockerKind.TargetUnavailable, "The target drive is missing or not ready.")];
        }

        var blockers = new List<RelocationBlocker>();
        if (volume.DriveType != DriveType.Fixed)
        {
            blockers.Add(new RelocationBlocker(BlockerKind.TargetNotFixedDrive,
                $"{volume.RootPath} is a {volume.DriveType} drive. Pick an internal drive that is always connected."));
        }
        if (!SupportedFileSystems.Contains(volume.FileSystem, StringComparer.OrdinalIgnoreCase))
        {
            blockers.Add(new RelocationBlocker(BlockerKind.TargetFileSystemUnsupported,
                $"{volume.RootPath} uses {volume.FileSystem}. Moved apps need an NTFS or ReFS drive."));
        }
        return blockers;
    }
}
