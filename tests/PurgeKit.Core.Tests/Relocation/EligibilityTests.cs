namespace PurgeKit.Core.Tests.Relocation;

public sealed class EligibilityTests : IDisposable
{
    private readonly RelocationTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task OrdinaryAppFolder_IsEligibleAndReportsItsSize()
    {
        var app = _bed.CreateApp();

        var report = await _bed.CreateService().CheckEligibilityAsync(_bed.RequestFor(app));

        report.IsEligible.Should().BeTrue();
        report.ToPath.Should().Be(_bed.TargetOf(app));
        report.SizeBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MissingFolder_IsBlocked()
    {
        var blockers = await _bed.BlockersForAsync(Path.Combine(_bed.SourceDrive, "Gone"));

        blockers.Should().Equal(BlockerKind.SourceNotFound);
    }

    [Fact]
    public async Task ProtectedFolder_IsBlocked()
    {
        var app = _bed.CreateApp();
        _bed.Policy.Protect(app);

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.ProtectedPath);
    }

    [Fact]
    public async Task AlreadyRelocatedFolder_IsBlocked()
    {
        var app = _bed.CreateApp();
        await _bed.RelocateAsync(app);

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.AlreadyRelocated);
    }

    [Fact]
    public async Task FolderContainingLink_IsBlocked()
    {
        var app = _bed.CreateApp();
        var elsewhere = Directory.CreateDirectory(Path.Combine(_bed.SourceDrive, "Elsewhere")).FullName;
        _bed.Junctions.Create(Path.Combine(app, "linked"), elsewhere);

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.ContainsLinks);
    }

    [Fact]
    public async Task FolderWithOpenFiles_IsBlocked()
    {
        var app = _bed.CreateApp();
        _bed.Locks.Lock(Path.Combine(app, RelocationTestBed.ExecutableName), "App");

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.InUse);
    }

    [Fact]
    public async Task FolderRunningAService_IsBlocked()
    {
        var app = _bed.CreateApp();
        _bed.Services.Register(Path.Combine(app, RelocationTestBed.ExecutableName), "AppUpdater");

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.ServiceRegistered);
    }

    [Fact]
    public async Task AdminFolder_WhenNotElevated_IsBlocked()
    {
        var app = _bed.CreateApp();
        _bed.Policy.RequireElevationFor(_bed.SourceDrive);
        _bed.Elevation.IsElevated = false;

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.RequiresElevation);
    }

    [Fact]
    public async Task AdminFolder_WhenElevated_IsEligible()
    {
        var app = _bed.CreateApp();
        _bed.Policy.RequireElevationFor(_bed.SourceDrive);

        (await _bed.BlockersForAsync(app)).Should().BeEmpty();
    }

    [Fact]
    public async Task TargetOnSameVolume_IsBlocked()
    {
        var app = _bed.CreateApp();
        _bed.Volumes.RemoveVolume(_bed.SourceDrive);
        _bed.Volumes.RemoveVolume(_bed.TargetDrive);
        _bed.Volumes.SetVolume(_bed.Root);

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.SameVolume);
    }

    [Fact]
    public async Task TargetWithoutRoomForFolderPlusHeadroom_IsBlocked()
    {
        var app = _bed.CreateApp();
        var report = await _bed.CreateService().CheckEligibilityAsync(_bed.RequestFor(app));
        _bed.Volumes.SetVolume(_bed.TargetDrive, freeBytes: report.SizeBytes);

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.InsufficientSpace);
    }

    [Fact]
    public async Task RemovableTarget_IsBlocked()
    {
        var app = _bed.CreateApp();
        _bed.Volumes.SetVolume(_bed.TargetDrive, DriveType.Removable);

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.TargetNotFixedDrive);
    }

    [Fact]
    public async Task ExFatTarget_IsBlocked()
    {
        var app = _bed.CreateApp();
        _bed.Volumes.SetVolume(_bed.TargetDrive, fileSystem: "exFAT");

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.TargetFileSystemUnsupported);
    }

    [Fact]
    public async Task MissingTargetDrive_IsBlocked()
    {
        var app = _bed.CreateApp();
        _bed.Volumes.RemoveVolume(_bed.TargetDrive);

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.TargetUnavailable);
    }

    [Fact]
    public async Task ExistingTargetFolder_IsBlocked()
    {
        var app = _bed.CreateApp();
        Directory.CreateDirectory(_bed.TargetOf(app));

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.TargetAlreadyExists);
    }

    [Fact]
    public async Task LeftoverFromInterruptedMove_IsBlocked()
    {
        var app = _bed.CreateApp();
        Directory.CreateDirectory(RelocationPaths.PendingPath(app));

        (await _bed.BlockersForAsync(app)).Should().Contain(BlockerKind.LeftoverArtifacts);
    }

    [Fact]
    public async Task TargetRootInsideSource_IsBlocked()
    {
        var app = _bed.CreateApp();
        var report = await _bed.CreateService().CheckEligibilityAsync(new RelocationRequest(app, Path.Combine(app, "nested")));

        report.Blockers.Select(b => b.Kind).Should().Contain(BlockerKind.TargetInsideSource);
    }
}
