namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Lists installed desktop apps.</summary>
public interface IInstalledAppSource
{
    /// <summary>Every installed desktop app Windows knows about.</summary>
    Task<IReadOnlyList<InstalledApp>> GetAppsAsync(CancellationToken ct = default);
}
