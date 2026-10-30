namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Where the Move Apps list gets its apps from.</summary>
/// <param name="Installed">Desktop apps.</param>
/// <param name="Store">Microsoft Store apps.</param>
public sealed record CandidateSources(IInstalledAppSource Installed, IStorePackageCatalog Store);
