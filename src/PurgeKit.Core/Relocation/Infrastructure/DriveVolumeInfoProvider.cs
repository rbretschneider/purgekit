using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Infrastructure;

/// <summary>Volume details from <see cref="DriveInfo"/>, keyed by drive letter.</summary>
public sealed class DriveVolumeInfoProvider : IVolumeInfoProvider
{
    /// <inheritdoc />
    public VolumeDetails? GetVolume(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        var drive = new DriveInfo(root);
        return drive.IsReady
            ? new VolumeDetails(drive.RootDirectory.FullName, drive.DriveType, drive.DriveFormat, drive.AvailableFreeSpace)
            : null;
    }
}
