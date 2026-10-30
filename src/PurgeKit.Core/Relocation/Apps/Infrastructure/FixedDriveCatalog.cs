namespace PurgeKit.Core.Relocation.Apps.Infrastructure;

/// <summary>Lists ready, fixed NTFS/ReFS drives other than the system drive.</summary>
public sealed class FixedDriveCatalog : ITargetDriveCatalog
{
    private static readonly string[] SupportedFileSystems = ["NTFS", "ReFS"];

    /// <inheritdoc />
    public IReadOnlyList<TargetDrive> ListTargets() =>
        DriveInfo.GetDrives()
            .Where(IsUsableTarget)
            .Select(drive => new TargetDrive(drive.RootDirectory.FullName, drive.VolumeLabel, drive.AvailableFreeSpace))
            .OrderByDescending(drive => drive.FreeBytes)
            .ToList();

    private static bool IsUsableTarget(DriveInfo drive) =>
        drive.DriveType == DriveType.Fixed
        && drive.IsReady
        && SupportedFileSystems.Contains(drive.DriveFormat, StringComparer.OrdinalIgnoreCase)
        && !RelocationPaths.AreSame(drive.RootDirectory.FullName, SystemDrive.Root);
}
