using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>Looks up the volume a path lives on.</summary>
public interface IVolumeInfoProvider
{
    /// <summary>Details of the volume holding <paramref name="path"/>, or null when it is missing or not ready.</summary>
    VolumeDetails? GetVolume(string path);
}
