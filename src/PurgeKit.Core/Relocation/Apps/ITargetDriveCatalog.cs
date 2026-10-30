namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Lists drives that can receive moved apps.</summary>
public interface ITargetDriveCatalog
{
    /// <summary>Fixed NTFS or ReFS drives other than the system drive.</summary>
    IReadOnlyList<TargetDrive> ListTargets();
}
