namespace PurgeKit.Core.Relocation.Apps;

/// <summary>An app that now lives off the system drive.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Kind">How it was moved.</param>
/// <param name="OriginalPath">Where it used to live (for Store apps, the system drive's app store).</param>
/// <param name="CurrentPath">Where its files are now.</param>
/// <param name="SizeBytes">Size when moved; 0 when unknown.</param>
/// <param name="MovedUtc">When it was moved; null when unknown.</param>
/// <param name="IsInterrupted">True when the move did not finish; PurgeKit settles it on next start.</param>
/// <param name="RelocationId">Journal id for junction moves.</param>
/// <param name="PackageFullName">Package full name for Store apps.</param>
public sealed record MovedApp(
    string Name,
    MovedAppKind Kind,
    string OriginalPath,
    string CurrentPath,
    long SizeBytes,
    DateTime? MovedUtc,
    bool IsInterrupted,
    Guid? RelocationId = null,
    string? PackageFullName = null);
