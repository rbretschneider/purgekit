using PurgeKit.Core.Relocation.Apps;
using PurgeKit.Core.Relocation.Infrastructure;
using PurgeKit.Core.Tests.Relocation.Apps.Fakes;

namespace PurgeKit.Core.Tests.Relocation.Apps;

/// <summary>Uses real folders under the temp directory, which lives on the system drive.</summary>
public sealed class AppCandidateProviderTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "purgekit-tests", Guid.NewGuid().ToString("N"))).FullName;
    private readonly FakeInstalledAppSource _installed = new();
    private readonly FakeStorePackageCatalog _store = new();
    private readonly NtfsJunctionManager _junctions = new();

    public void Dispose() => DirectoryDeletion.DeleteTree(_root);

    [Fact]
    public async Task ListsInstalledAppWithRealFolderOnSystemDrive()
    {
        var path = Folder("Blender");
        _installed.Apps.Add(App("Blender", path, sizeBytes: 5000));

        var candidate = (await GetAsync()).Items.Should().ContainSingle().Subject;

        candidate.Name.Should().Be("Blender");
        candidate.SizeBytes.Should().Be(5000);
        candidate.Option.Should().Be(AppMoveOption.Movable);
    }

    [Fact]
    public async Task MeasuresSizeWhenInstallerDidNotRecordOne()
    {
        _installed.Apps.Add(App("Blender", Folder("Blender"), sizeBytes: 0));

        (await GetAsync()).Items.Single().SizeBytes.Should().Be(FixedSizeEstimator.Size);
    }

    [Fact]
    public async Task SkipsAppsWithoutUsableInstallFolder()
    {
        _installed.Apps.Add(App("No folder", null));
        _installed.Apps.Add(App("Deleted", Path.Combine(_root, "gone")));
        _installed.Apps.Add(App("Relative", @"Program Files\Thing"));

        (await GetAsync()).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SkipsAppsAlreadyMovedBehindAJunction()
    {
        var target = Folder("Elsewhere");
        var link = Path.Combine(_root, "Moved");
        _junctions.Create(link, target);
        _installed.Apps.Add(App("Moved", link));

        (await GetAsync()).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListsSharedInstallFolderOnce()
    {
        var path = Folder("Suite");
        _installed.Apps.Add(App("Suite", path));
        _installed.Apps.Add(App("Suite (x64 component)", path + Path.DirectorySeparatorChar));

        (await GetAsync()).Items.Should().ContainSingle();
    }

    [Fact]
    public async Task IncludesStoreAppsStillOnSystemDrive()
    {
        _store.Packages.Add(new StorePackage("A_1_x64__b", "A", Folder("WindowsApps-A")));

        var candidate = (await GetAsync()).Items.Should().ContainSingle().Subject;

        candidate.Option.Should().Be(AppMoveOption.StoreApp);
        candidate.PackageFullName.Should().Be("A_1_x64__b");
    }

    [Fact]
    public async Task WhenStoreAppsCannotBeListed_StillListsDesktopAppsWithWarning()
    {
        _installed.Apps.Add(App("Blender", Folder("Blender")));
        _store.IsBroken = true;

        var result = await GetAsync();

        result.Items.Should().ContainSingle();
        result.Warning.Should().Contain("Store");
    }

    private Task<ListResult<AppCandidate>> GetAsync() =>
        new AppCandidateProvider(new CandidateSources(_installed, _store), new AppClassifier([]), new FixedSizeEstimator(), _junctions)
            .GetCandidatesAsync();

    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_root, name)).FullName;

    private static InstalledApp App(string name, string? path, long sizeBytes = 1) =>
        new(name, "Publisher", path, sizeBytes, null, null);
}
