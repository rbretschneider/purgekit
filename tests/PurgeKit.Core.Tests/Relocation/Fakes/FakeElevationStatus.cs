namespace PurgeKit.Core.Tests.Relocation.Fakes;

internal sealed class FakeElevationStatus : IElevationStatus
{
    public bool IsElevated { get; set; } = true;
}
