using PurgeKit.Core.Relocation.Apps;

namespace PurgeKit.Core.Tests.Relocation.Apps.Fakes;

/// <summary>Reports the same size for every folder.</summary>
internal sealed class FixedSizeEstimator : IFolderSizeEstimator
{
    public const long Size = 123_456_789;

    public long EstimateBytes(string path, CancellationToken ct = default) => Size;
}
