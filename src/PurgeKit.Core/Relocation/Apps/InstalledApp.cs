namespace PurgeKit.Core.Relocation.Apps;

/// <summary>A desktop app as reported by Windows' list of installed programs.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Publisher">Publisher, or empty.</param>
/// <param name="InstallPath">Install folder, or null when the app does not record one.</param>
/// <param name="EstimatedSizeBytes">Size reported by the installer; 0 when unknown.</param>
/// <param name="LastUsed">Best guess at when it was last run, or null when unknown.</param>
/// <param name="LastUsedSource">Where <paramref name="LastUsed"/> came from (Prefetch, AppData, ...).</param>
public sealed record InstalledApp(
    string Name,
    string Publisher,
    string? InstallPath,
    long EstimatedSizeBytes,
    DateTime? LastUsed,
    string? LastUsedSource);
