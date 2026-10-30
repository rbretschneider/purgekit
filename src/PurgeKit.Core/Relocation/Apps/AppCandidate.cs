namespace PurgeKit.Core.Relocation.Apps;

/// <summary>An app on the system drive, with the verdict on whether and how it can be moved.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Publisher">Publisher, or empty.</param>
/// <param name="InstallPath">Install folder.</param>
/// <param name="SizeBytes">Size on disk (installer estimate, or measured).</param>
/// <param name="LastUsed">Best guess at when it was last run, or null when unknown.</param>
/// <param name="LastUsedSource">Where <paramref name="LastUsed"/> came from.</param>
/// <param name="Option">Whether and how it can be moved.</param>
/// <param name="Reason">Plain-English explanation of <paramref name="Option"/>.</param>
/// <param name="PackageFullName">Store package full name; null for desktop apps.</param>
public sealed record AppCandidate(
    string Name,
    string Publisher,
    string InstallPath,
    long SizeBytes,
    DateTime? LastUsed,
    string? LastUsedSource,
    AppMoveOption Option,
    string Reason,
    string? PackageFullName = null)
{
    /// <summary>True when PurgeKit can move this app (itself or through Windows).</summary>
    public bool CanMove => Option is AppMoveOption.Movable or AppMoveOption.StoreApp;
}
