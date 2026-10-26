using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>Decides whether a completed relocation can be moved back.</summary>
public interface IRestoreEligibilityChecker
{
    /// <summary>Checks the junction, open files and free space for restoring <paramref name="entry"/>.</summary>
    Task<EligibilityReport> CheckAsync(RelocationEntry entry, CancellationToken ct = default);
}
