using PurgeKit.Core.Relocation.Apps;
using PurgeKit.Core.Tests.Relocation.Apps.Fakes;
using PurgeKit.Core.Tests.Relocation.Fakes;

namespace PurgeKit.Core.Tests.Relocation.Apps;

public sealed class AppMoverTests
{
    private static readonly TargetDrive DriveD = new(@"D:\", "Data", FakeVolumeInfoProvider.PlentyOfSpace);

    private readonly RecordingRelocationService _relocation = new();
    private readonly FakeStorePackageCatalog _store = new();
    private readonly FakeVolumeInfoProvider _volumes = new();
    private readonly AppMover _mover;

    public AppMoverTests()
    {
        _volumes.SetVolume(SystemDrive.Root);
        _mover = new AppMover(_relocation, _store, _volumes);
    }

    [Fact]
    public async Task DesktopApp_IsRelocatedUnderDefaultRootOfChosenDrive()
    {
        await _mover.MoveAsync(Candidate(AppMoveOption.Movable), DriveD);

        var request = _relocation.Relocations.Should().ContainSingle().Subject;
        request.TargetRoot.Should().Be(@"D:\PurgeKit\Relocated");
        request.DisplayName.Should().Be("Blender");
    }

    [Fact]
    public async Task StoreApp_IsMovedByWindowsToChosenDrive()
    {
        await _mover.MoveAsync(Candidate(AppMoveOption.StoreApp, "A_1_x64__b"), DriveD);

        _store.Moves.Should().Equal(("A_1_x64__b", @"D:\"));
        _relocation.Relocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(AppMoveOption.Blocked)]
    [InlineData(AppMoveOption.UseLauncher)]
    public async Task UnmovableApp_IsRefused(AppMoveOption option)
    {
        var act = () => _mover.MoveAsync(Candidate(option), DriveD);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StoreApp_TooBigForDrive_IsReportedAsBlocked()
    {
        var report = await _mover.CheckMoveAsync(Candidate(AppMoveOption.StoreApp, "A_1_x64__b", size: 1000), DriveD with { FreeBytes = 1000 });

        report.Blockers.Should().ContainSingle().Which.Kind.Should().Be(BlockerKind.InsufficientSpace);
    }

    [Fact]
    public async Task MovedApps_CombineJournalAndStoreAppsOffSystemDrive()
    {
        _relocation.Entries.Add(new RelocationEntry(Guid.NewGuid(), @"C:\Apps\Blender", @"D:\PurgeKit\Relocated\C\Apps\Blender",
            RelocationState.Completed, 10, DateTime.UtcNow, DateTime.UtcNow, "Blender"));
        _store.Packages.Add(new StorePackage("A_1_x64__b", "A", @"D:\WindowsApps\A_1_x64__b"));
        _store.Packages.Add(new StorePackage("B_1_x64__b", "B", Path.Combine(SystemDrive.Root, "Program Files", "WindowsApps", "B")));

        var moved = (await _mover.GetMovedAppsAsync()).Items;

        moved.Select(m => (m.Name, m.Kind)).Should().Equal(("A", MovedAppKind.StorePackage), ("Blender", MovedAppKind.Junction));
    }

    [Fact]
    public async Task MovedApps_WhenStoreUnavailable_StillListsOwnMovesWithWarning()
    {
        _relocation.Entries.Add(new RelocationEntry(Guid.NewGuid(), @"C:\Apps\Blender", @"D:\R\C\Apps\Blender",
            RelocationState.Completed, 10, DateTime.UtcNow, DateTime.UtcNow));
        _store.IsBroken = true;

        var result = await _mover.GetMovedAppsAsync();

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Blender");
        result.Warning.Should().NotBeNull();
    }

    [Fact]
    public async Task MoveBack_JunctionMove_RestoresByJournalId()
    {
        var id = Guid.NewGuid();

        await _mover.MoveBackAsync(new MovedApp("Blender", MovedAppKind.Junction, @"C:\A", @"D:\A", 0, null, false, RelocationId: id));

        _relocation.Restores.Should().Equal(id);
    }

    [Fact]
    public async Task MoveBack_StoreApp_MovesToSystemDrive()
    {
        await _mover.MoveBackAsync(new MovedApp("A", MovedAppKind.StorePackage, SystemDrive.Root, @"D:\WindowsApps\A", 0, null, false, PackageFullName: "A_1_x64__b"));

        _store.Moves.Should().Equal(("A_1_x64__b", SystemDrive.Root));
    }

    private static AppCandidate Candidate(AppMoveOption option, string? package = null, long size = 100) =>
        new("Blender", "Blender Foundation", @"C:\Program Files\Blender", size, null, null, option, "", package);
}
