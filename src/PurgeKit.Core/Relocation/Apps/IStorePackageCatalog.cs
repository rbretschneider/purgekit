namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Lists and moves Microsoft Store apps through Windows' own app mover.</summary>
public interface IStorePackageCatalog
{
    /// <summary>User-installed Store apps that Windows allows to be moved (no frameworks or system apps).</summary>
    Task<IReadOnlyList<StorePackage>> ListPackagesAsync(CancellationToken ct = default);

    /// <summary>Moves a package to <paramref name="driveRoot"/>; the system drive moves it back.</summary>
    /// <exception cref="StorePackageException">Windows refused or failed the move.</exception>
    Task MovePackageAsync(string packageFullName, string driveRoot, CancellationToken ct = default);
}
