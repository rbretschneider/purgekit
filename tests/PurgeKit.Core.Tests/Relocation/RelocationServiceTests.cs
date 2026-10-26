using PurgeKit.Core.Relocation.Infrastructure;
using PurgeKit.Core.Tests.Relocation.Fakes;

namespace PurgeKit.Core.Tests.Relocation;

public sealed class RelocationServiceTests : IDisposable
{
    private readonly RelocationTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task RelocateAsync_LeavesJunctionAtOriginalPathPointingToTarget()
    {
        var app = _bed.CreateApp();

        await _bed.RelocateAsync(app);

        _bed.Junctions.GetTarget(app).Should().Be(_bed.TargetOf(app));
    }

    [Fact]
    public async Task RelocateAsync_KeepsFilesReadableThroughOriginalPath()
    {
        var app = _bed.CreateApp();

        await _bed.RelocateAsync(app);

        File.ReadAllText(Path.Combine(app, RelocationTestBed.SettingsFile)).Should().Be(RelocationTestBed.SettingsContent);
    }

    [Fact]
    public async Task RelocateAsync_RemovesOriginalFilesFromSourceDrive()
    {
        var app = _bed.CreateApp();

        await _bed.RelocateAsync(app);

        Directory.Exists(RelocationPaths.PendingPath(app)).Should().BeFalse();
        Directory.EnumerateFileSystemEntries(_bed.SourceDrive).Should().ContainSingle().Which.Should().Be(app);
    }

    [Fact]
    public async Task RelocateAsync_PreservesReadOnlyAttributeAndTimestamps()
    {
        var app = _bed.CreateApp();

        await _bed.RelocateAsync(app);

        var moved = new FileInfo(Path.Combine(_bed.TargetOf(app), RelocationTestBed.ReadOnlyFile));
        moved.Attributes.Should().HaveFlag(FileAttributes.ReadOnly);
        moved.LastWriteTimeUtc.Should().Be(RelocationTestBed.ExpectedReadOnlyWriteTimeUtc);
    }

    [Fact]
    public async Task RelocateAsync_RecordsCompletedEntryWithSize()
    {
        var app = _bed.CreateApp();

        var entry = await _bed.RelocateAsync(app);

        entry.State.Should().Be(RelocationState.Completed);
        entry.SizeBytes.Should().BeGreaterThan(0);
        (await _bed.SingleEntryAsync()).Should().Be(entry);
    }

    [Fact]
    public async Task RelocateAsync_ReportsStagesInOrder()
    {
        var app = _bed.CreateApp();
        var progress = new CallbackProgress<RelocationProgress>(_ => { });

        await _bed.CreateService().RelocateAsync(_bed.RequestFor(app), progress);

        progress.Reports.Select(r => r.Stage).Distinct().Should().Equal(
            RelocationStage.Checking, RelocationStage.Copying, RelocationStage.Verifying,
            RelocationStage.Swapping, RelocationStage.Cleaning);
    }

    [Fact]
    public async Task RelocateAsync_WhenBlocked_ThrowsWithReportAndChangesNothing()
    {
        var app = _bed.CreateApp();
        _bed.Locks.Lock(Path.Combine(app, RelocationTestBed.ExecutableName), "App");

        var act = () => _bed.RelocateAsync(app);

        (await act.Should().ThrowAsync<RelocationBlockedException>())
            .Which.Report.Blockers.Should().ContainSingle(b => b.Kind == BlockerKind.InUse);
        await AssertUntouchedAsync(app);
    }

    [Fact]
    public async Task RelocateAsync_WhenCopyFails_RestoresOriginalState()
    {
        var app = _bed.CreateApp();
        _bed.Copier = new FailingCopier(new AclPreservingDirectoryCopier());

        var act = () => _bed.RelocateAsync(app);

        await act.Should().ThrowAsync<IOException>();
        await AssertUntouchedAsync(app);
    }

    [Fact]
    public async Task RelocateAsync_WhenCopyDoesNotMatch_RollsBackWithVerificationError()
    {
        var app = _bed.CreateApp();
        _bed.Copier = new CorruptingCopier(new AclPreservingDirectoryCopier(), RelocationTestBed.ExecutableName);

        var act = () => _bed.RelocateAsync(app);

        (await act.Should().ThrowAsync<RelocationVerificationException>())
            .Which.Mismatches.Should().ContainSingle().Which.Should().Contain(RelocationTestBed.ExecutableName);
        await AssertUntouchedAsync(app);
    }

    [Fact]
    public async Task RelocateAsync_WhenCancelledDuringCopy_RollsBack()
    {
        var app = _bed.CreateApp();
        using var cts = new CancellationTokenSource();
        var progress = new CancelOnFirstCopyProgress(cts);

        var act = () => _bed.CreateService().RelocateAsync(_bed.RequestFor(app), progress, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await AssertUntouchedAsync(app);
    }

    [Fact]
    public async Task RestoreAsync_PutsRealFolderBackWithOriginalContents()
    {
        var app = _bed.CreateApp();
        var entry = await _bed.RelocateAsync(app);

        await _bed.CreateService().RestoreAsync(entry.Id);

        _bed.Junctions.IsLink(app).Should().BeFalse();
        File.ReadAllText(Path.Combine(app, RelocationTestBed.SettingsFile)).Should().Be(RelocationTestBed.SettingsContent);
    }

    [Fact]
    public async Task RestoreAsync_RemovesRelocatedCopyAndJournalEntry()
    {
        var app = _bed.CreateApp();
        var entry = await _bed.RelocateAsync(app);

        await _bed.CreateService().RestoreAsync(entry.Id);

        Directory.Exists(entry.TargetPath).Should().BeFalse();
        (await _bed.Journal.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RestoreAsync_WhenRelocatedFolderInUse_ThrowsAndKeepsRelocation()
    {
        var app = _bed.CreateApp();
        var entry = await _bed.RelocateAsync(app);
        _bed.Locks.Lock(Path.Combine(entry.TargetPath, RelocationTestBed.ExecutableName), "App");

        var act = () => _bed.CreateService().RestoreAsync(entry.Id);

        await act.Should().ThrowAsync<RelocationBlockedException>();
        _bed.Junctions.GetTarget(app).Should().Be(entry.TargetPath);
        (await _bed.SingleEntryAsync()).State.Should().Be(RelocationState.Completed);
    }

    [Fact]
    public async Task RestoreAsync_WhenCancelledDuringCopy_KeepsRelocationIntact()
    {
        var app = _bed.CreateApp();
        var entry = await _bed.RelocateAsync(app);
        using var cts = new CancellationTokenSource();

        var act = () => _bed.CreateService().RestoreAsync(entry.Id, new CancelOnFirstCopyProgress(cts), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _bed.Junctions.GetTarget(app).Should().Be(entry.TargetPath);
        Directory.Exists(RelocationPaths.RestoringPath(app)).Should().BeFalse();
        (await _bed.SingleEntryAsync()).State.Should().Be(RelocationState.Completed);
    }

    [Fact]
    public async Task RestoreAsync_WithUnknownId_ThrowsKeyNotFound()
    {
        var act = () => _bed.CreateService().RestoreAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task AssertUntouchedAsync(string app)
    {
        _bed.Junctions.IsLink(app).Should().BeFalse();
        File.ReadAllText(Path.Combine(app, RelocationTestBed.SettingsFile)).Should().Be(RelocationTestBed.SettingsContent);
        Directory.Exists(_bed.TargetOf(app)).Should().BeFalse();
        Directory.Exists(RelocationPaths.PendingPath(app)).Should().BeFalse();
        (await _bed.Journal.GetAllAsync()).Should().BeEmpty();
    }
}
