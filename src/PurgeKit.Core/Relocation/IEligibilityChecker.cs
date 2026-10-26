using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>Decides whether a folder can be relocated.</summary>
public interface IEligibilityChecker
{
    /// <summary>Runs every eligibility rule against <paramref name="request"/>.</summary>
    Task<EligibilityReport> CheckAsync(RelocationRequest request, CancellationToken ct = default);
}
