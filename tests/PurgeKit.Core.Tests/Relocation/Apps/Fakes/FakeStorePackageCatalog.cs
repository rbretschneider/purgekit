using PurgeKit.Core.Relocation.Apps;

namespace PurgeKit.Core.Tests.Relocation.Apps.Fakes;

internal sealed class FakeStorePackageCatalog : IStorePackageCatalog
{
    public List<StorePackage> Packages { get; } = [];

    public bool IsBroken { get; set; }

    public List<(string FullName, string DriveRoot)> Moves { get; } = [];

    public Task<IReadOnlyList<StorePackage>> ListPackagesAsync(CancellationToken ct = default) =>
        IsBroken
            ? Task.FromException<IReadOnlyList<StorePackage>>(new StorePackageException("Appx module unavailable"))
            : Task.FromResult<IReadOnlyList<StorePackage>>(Packages);

    public Task MovePackageAsync(string packageFullName, string driveRoot, CancellationToken ct = default)
    {
        Moves.Add((packageFullName, driveRoot));
        return Task.CompletedTask;
    }
}
