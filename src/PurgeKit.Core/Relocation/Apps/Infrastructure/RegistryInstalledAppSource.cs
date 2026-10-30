using PurgeKit.Core.Scanning;

namespace PurgeKit.Core.Relocation.Apps.Infrastructure;

/// <summary>Installed desktop apps from the existing <see cref="InstalledProgramsScanner"/>.</summary>
public sealed class RegistryInstalledAppSource : IInstalledAppSource
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<InstalledApp>> GetAppsAsync(CancellationToken ct = default)
    {
        var programs = await new InstalledProgramsScanner().ScanAsync(ct).ConfigureAwait(false);
        return programs
            .Select(p => new InstalledApp(p.DisplayName, p.Publisher, p.InstallLocation, p.EstimatedSizeBytes, p.LastUsed, p.LastUsedSource))
            .ToList();
    }
}
