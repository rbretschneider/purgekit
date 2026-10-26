using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>
/// Moves folders to another drive and leaves a junction behind, so every path that pointed at the
/// original keeps working. Every move is journaled and can be undone with <see cref="RestoreAsync"/>.
/// </summary>
public interface IRelocationService
{
    /// <summary>Reports whether <paramref name="request"/> can be carried out, and why not if it can't.</summary>
    Task<EligibilityReport> CheckEligibilityAsync(RelocationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Copies the folder to the target, verifies it, and replaces the original with a junction.
    /// Cancelling or failing at any point before the swap leaves the original untouched.
    /// </summary>
    /// <exception cref="RelocationBlockedException">The request is not eligible.</exception>
    Task<RelocationEntry> RelocateAsync(
        RelocationRequest request,
        IProgress<RelocationProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>Reports whether a completed relocation can be moved back right now.</summary>
    /// <exception cref="KeyNotFoundException">No relocation has this id.</exception>
    Task<EligibilityReport> CheckRestoreAsync(Guid relocationId, CancellationToken ct = default);

    /// <summary>Copies a relocated folder back and replaces the junction with the real folder.</summary>
    /// <exception cref="KeyNotFoundException">No relocation has this id.</exception>
    /// <exception cref="RelocationBlockedException">The restore is not possible right now.</exception>
    Task RestoreAsync(
        Guid relocationId,
        IProgress<RelocationProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>All journaled relocations, including any interrupted ones.</summary>
    Task<IReadOnlyList<RelocationEntry>> GetRelocationsAsync(CancellationToken ct = default);
}
