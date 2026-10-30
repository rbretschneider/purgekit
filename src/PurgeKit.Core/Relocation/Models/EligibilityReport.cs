namespace PurgeKit.Core.Relocation.Models;

/// <summary>Outcome of checking whether a folder can be moved from one place to another.</summary>
/// <param name="FromPath">Folder whose contents would be copied.</param>
/// <param name="ToPath">Where the contents would end up.</param>
/// <param name="SizeBytes">Bytes that would be copied.</param>
/// <param name="Blockers">Everything preventing the move; empty when it can proceed.</param>
/// <param name="DisplayName">Friendly name of what would be moved, carried through to the journal.</param>
public sealed record EligibilityReport(
    string FromPath,
    string ToPath,
    long SizeBytes,
    IReadOnlyList<RelocationBlocker> Blockers,
    string? DisplayName = null)
{
    /// <summary>True when nothing blocks the move.</summary>
    public bool IsEligible => Blockers.Count == 0;
}
