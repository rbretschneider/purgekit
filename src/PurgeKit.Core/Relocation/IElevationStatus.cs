namespace PurgeKit.Core.Relocation;

/// <summary>Whether the current process runs with administrator rights.</summary>
public interface IElevationStatus
{
    /// <summary>True when running elevated.</summary>
    bool IsElevated { get; }
}
