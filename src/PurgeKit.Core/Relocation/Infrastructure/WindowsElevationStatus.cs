using PurgeKit.Core.Services;

namespace PurgeKit.Core.Relocation.Infrastructure;

/// <summary>Elevation status of the current Windows process.</summary>
public sealed class WindowsElevationStatus : IElevationStatus
{
    /// <inheritdoc />
    public bool IsElevated => ElevationHelper.IsRunningAsAdmin();
}
