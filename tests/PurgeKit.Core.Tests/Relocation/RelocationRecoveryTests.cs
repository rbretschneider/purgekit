using PurgeKit.Core.Relocation.Infrastructure;

namespace PurgeKit.Core.Tests.Relocation;

/// <summary>Each test recreates the on-disk state a crash at a given step would leave, then recovers.</summary>
public sealed class RelocationRecoveryTests : IDisposable
{
    private readonly RelocationTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task CrashDuringCopy_DiscardsPartialCopyAndKeepsOriginal()
    {
        var app = _bed.CreateApp();
        Directory.CreateDirectory(_bed.TargetOf(app));
        File.WriteAllText(Path.Combine(_bed.TargetOf(app), "partial.bin"), "half");
        await _bed.SaveEntryAsync(app, RelocationState.Copying);

        await _bed.CreateRecovery().RecoverAsync();

        await AssertOriginalBackAsync(app);
    }

    [Fact]
    public async Task CrashAfterRenamingOriginalAside_PutsOriginalBack()
    {
        var app = _bed.CreateApp();
        await CopyToTargetAsync(app);
        Directory.Move(app, RelocationPaths.PendingPath(app));
        await _bed.SaveEntryAsync(app, RelocationState.Verified);

        await _bed.CreateRecovery().RecoverAsync();

        await AssertOriginalBackAsync(app);
    }

    [Fact]
    public async Task CrashAfterJunctionBeforeJournalUpdate_PutsOriginalBack()
    {
        var app = _bed.CreateApp();
        await CopyToTargetAsync(app);
        Directory.Move(app, RelocationPaths.PendingPath(app));
        _bed.Junctions.Create(app, _bed.TargetOf(app));
        await _bed.SaveEntryAsync(app, RelocationState.Verified);

        await _bed.CreateRecovery().RecoverAsync();

        await AssertOriginalBackAsync(app);
    }

    [Fact]
    public async Task CrashAfterSwap_FinishesRelocation()
    {
        var app = _bed.CreateApp();
        await CopyToTargetAsync(app);
        Directory.Move(app, RelocationPaths.PendingPath(app));
        _bed.Junctions.Create(app, _bed.TargetOf(app));
        await _bed.SaveEntryAsync(app, RelocationState.Swapped);

        await _bed.CreateRecovery().RecoverAsync();

        Directory.Exists(RelocationPaths.PendingPath(app)).Should().BeFalse();
        _bed.Junctions.GetTarget(app).Should().Be(_bed.TargetOf(app));
        (await _bed.SingleEntryAsync()).State.Should().Be(RelocationState.Completed);
    }

    [Fact]
    public async Task CrashDuringRestoreCopy_KeepsRelocation()
    {
        var app = _bed.CreateApp();
        var entry = await _bed.RelocateAsync(app);
        Directory.CreateDirectory(RelocationPaths.RestoringPath(app));
        await _bed.Journal.SaveAsync(entry with { State = RelocationState.Restoring });

        await _bed.CreateRecovery().RecoverAsync();

        Directory.Exists(RelocationPaths.RestoringPath(app)).Should().BeFalse();
        _bed.Junctions.GetTarget(app).Should().Be(entry.TargetPath);
        (await _bed.SingleEntryAsync()).State.Should().Be(RelocationState.Completed);
    }

    [Fact]
    public async Task CrashAfterJunctionRemovedDuringRestore_RecreatesJunction()
    {
        var app = _bed.CreateApp();
        var entry = await _bed.RelocateAsync(app);
        await new AclPreservingDirectoryCopier().CopyAsync(entry.TargetPath, RelocationPaths.RestoringPath(app), null, default);
        _bed.Junctions.Remove(app);
        await _bed.Journal.SaveAsync(entry with { State = RelocationState.RestoreVerified });

        await _bed.CreateRecovery().RecoverAsync();

        _bed.Junctions.GetTarget(app).Should().Be(entry.TargetPath);
        Directory.Exists(RelocationPaths.RestoringPath(app)).Should().BeFalse();
        (await _bed.SingleEntryAsync()).State.Should().Be(RelocationState.Completed);
    }

    [Fact]
    public async Task CrashAfterRealFolderRestored_FinishesRestore()
    {
        var app = _bed.CreateApp();
        var entry = await _bed.RelocateAsync(app);
        await new AclPreservingDirectoryCopier().CopyAsync(entry.TargetPath, RelocationPaths.RestoringPath(app), null, default);
        _bed.Junctions.Remove(app);
        Directory.Move(RelocationPaths.RestoringPath(app), app);
        await _bed.Journal.SaveAsync(entry with { State = RelocationState.RestoreVerified });

        await _bed.CreateRecovery().RecoverAsync();

        await AssertOriginalBackAsync(app);
    }

    [Fact]
    public async Task CompletedRelocations_AreLeftAlone()
    {
        var app = _bed.CreateApp();
        var entry = await _bed.RelocateAsync(app);

        var recovered = await _bed.CreateRecovery().RecoverAsync();

        recovered.Should().BeEmpty();
        (await _bed.SingleEntryAsync()).Should().Be(entry);
    }

    [Fact]
    public async Task WhenOriginalIsMissing_RefusesToDeleteTheOnlyCopy()
    {
        var app = Path.Combine(_bed.SourceDrive, "App");
        Directory.CreateDirectory(_bed.TargetOf(app));
        File.WriteAllText(Path.Combine(_bed.TargetOf(app), "only-copy.txt"), "precious");
        await _bed.SaveEntryAsync(app, RelocationState.Copying);

        var act = () => _bed.CreateRecovery().RecoverAsync();

        await act.Should().ThrowAsync<AggregateException>();
        File.Exists(Path.Combine(_bed.TargetOf(app), "only-copy.txt")).Should().BeTrue();
        (await _bed.Journal.GetAllAsync()).Should().ContainSingle();
    }

    private Task CopyToTargetAsync(string app) =>
        new AclPreservingDirectoryCopier().CopyAsync(app, _bed.TargetOf(app), null, default);

    private async Task AssertOriginalBackAsync(string app)
    {
        _bed.Junctions.IsLink(app).Should().BeFalse();
        File.ReadAllText(Path.Combine(app, RelocationTestBed.SettingsFile)).Should().Be(RelocationTestBed.SettingsContent);
        Directory.Exists(RelocationPaths.PendingPath(app)).Should().BeFalse();
        Directory.Exists(_bed.TargetOf(app)).Should().BeFalse();
        (await _bed.Journal.GetAllAsync()).Should().BeEmpty();
    }
}
