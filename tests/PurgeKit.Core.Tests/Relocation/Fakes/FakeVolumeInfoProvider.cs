namespace PurgeKit.Core.Tests.Relocation.Fakes;

/// <summary>Pretends folders are separate volumes so cross-drive moves can be tested on one disk.</summary>
internal sealed class FakeVolumeInfoProvider : IVolumeInfoProvider
{
    public const long PlentyOfSpace = 1L << 40;

    private readonly Dictionary<string, VolumeDetails> _volumes = new(StringComparer.OrdinalIgnoreCase);

    public void SetVolume(string root, DriveType driveType = DriveType.Fixed, string fileSystem = "NTFS", long freeBytes = PlentyOfSpace) =>
        _volumes[root] = new VolumeDetails(root, driveType, fileSystem, freeBytes);

    public void RemoveVolume(string root) => _volumes.Remove(root);

    public VolumeDetails? GetVolume(string path) =>
        _volumes.Values
            .Where(volume => RelocationPaths.IsSameOrUnder(path, volume.RootPath))
            .MaxBy(volume => volume.RootPath.Length);
}
