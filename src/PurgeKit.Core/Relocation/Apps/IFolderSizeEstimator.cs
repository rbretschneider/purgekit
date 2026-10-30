namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Estimates folder sizes for display. Unreadable parts are skipped, so results are lower bounds.</summary>
public interface IFolderSizeEstimator
{
    /// <summary>Approximate bytes under <paramref name="path"/>.</summary>
    long EstimateBytes(string path, CancellationToken ct = default);
}
