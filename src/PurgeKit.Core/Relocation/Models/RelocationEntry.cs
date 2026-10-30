namespace PurgeKit.Core.Relocation.Models;

/// <summary>
/// One journaled relocation: where the folder used to live, where it lives now, and how far the
/// move (or the move back) has progressed.
/// </summary>
/// <param name="Id">Stable identifier used to restore the relocation later.</param>
/// <param name="SourcePath">Original location; becomes a junction once relocated.</param>
/// <param name="TargetPath">Location on the target drive that holds the real files.</param>
/// <param name="State">Current lifecycle state.</param>
/// <param name="SizeBytes">Size of the folder when it was relocated.</param>
/// <param name="CreatedUtc">When the relocation started.</param>
/// <param name="UpdatedUtc">When the state last changed.</param>
/// <param name="DisplayName">Friendly name of what was moved, e.g. the app's name; null when unknown.</param>
public sealed record RelocationEntry(
    Guid Id,
    string SourcePath,
    string TargetPath,
    RelocationState State,
    long SizeBytes,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string? DisplayName = null);
