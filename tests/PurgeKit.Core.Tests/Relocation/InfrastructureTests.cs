using PurgeKit.Core.Relocation.Infrastructure;

namespace PurgeKit.Core.Tests.Relocation;

/// <summary>Contract tests for the real Windows implementations, run against a temp folder.</summary>
public sealed class InfrastructureTests : IDisposable
{
    private readonly RelocationTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public void Junction_ResolvesToTargetAndRemovingItKeepsTargetContents()
    {
        var target = _bed.CreateApp("Target");
        var link = Path.Combine(_bed.SourceDrive, "Link");

        _bed.Junctions.Create(link, target);
        File.ReadAllText(Path.Combine(link, RelocationTestBed.SettingsFile)).Should().Be(RelocationTestBed.SettingsContent);
        _bed.Junctions.Remove(link);

        Directory.Exists(link).Should().BeFalse();
        File.Exists(Path.Combine(target, RelocationTestBed.SettingsFile)).Should().BeTrue();
    }

    [Fact]
    public void Junction_RefusesToReplaceExistingFolder()
    {
        var target = _bed.CreateApp("Target");
        var existing = _bed.CreateApp("Existing");

        var act = () => _bed.Junctions.Create(existing, target);

        act.Should().Throw<IOException>();
    }

    [Fact]
    public void Junction_RefusesToRemoveRealFolder()
    {
        var app = _bed.CreateApp();

        var act = () => _bed.Junctions.Remove(app);

        act.Should().Throw<IOException>();
        Directory.Exists(app).Should().BeTrue();
    }

    [Fact]
    public async Task Copier_ReportsNoMismatchesForFaithfulCopy()
    {
        var app = _bed.CreateApp();
        var copy = Path.Combine(_bed.TargetDrive, "Copy");
        var copier = new AclPreservingDirectoryCopier();

        await copier.CopyAsync(app, copy, null, default);

        (await copier.FindMismatchesAsync(app, copy, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Copier_DetectsSameSizeContentChangeInExecutable()
    {
        var app = _bed.CreateApp();
        var copy = Path.Combine(_bed.TargetDrive, "Copy");
        var copier = new AclPreservingDirectoryCopier();
        await copier.CopyAsync(app, copy, null, default);
        var exe = Path.Combine(copy, RelocationTestBed.ExecutableName);
        var bytes = File.ReadAllBytes(exe);
        bytes[0] ^= 0xFF;
        File.WriteAllBytes(exe, bytes);

        (await copier.FindMismatchesAsync(app, copy, default)).Should().ContainSingle()
            .Which.Should().StartWith("Content differs");
    }

    [Fact]
    public async Task Copier_DetectsMissingAndUnexpectedFiles()
    {
        var app = _bed.CreateApp();
        var copy = Path.Combine(_bed.TargetDrive, "Copy");
        var copier = new AclPreservingDirectoryCopier();
        await copier.CopyAsync(app, copy, null, default);
        File.Delete(Path.Combine(copy, RelocationTestBed.SettingsFile));
        File.WriteAllText(Path.Combine(copy, "extra.txt"), "x");

        (await copier.FindMismatchesAsync(app, copy, default)).Should().HaveCount(2);
    }

    [Fact]
    public void Inspector_CountsFilesAndReportsLinksWithoutFollowingThem()
    {
        var app = _bed.CreateApp();
        var big = _bed.CreateApp("Big");
        File.WriteAllBytes(Path.Combine(big, "huge.bin"), new byte[1024 * 1024]);
        _bed.Junctions.Create(Path.Combine(app, "linked"), big);

        var profile = new FileSystemDirectoryInspector().Inspect(app, default);

        profile.FileCount.Should().Be(3);
        profile.SizeBytes.Should().BeLessThan(1024 * 1024);
        profile.LinkPaths.Should().ContainSingle().Which.Should().EndWith("linked");
    }

    [Fact]
    public void LockDetector_FindsThisProcessHoldingALibraryOpen()
    {
        var app = _bed.CreateApp();
        var library = Path.Combine(app, "plugin.dll");
        File.WriteAllBytes(library, new byte[16]);
        var detector = new RestartManagerLockDetector();

        using (new FileStream(library, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            detector.FindLockingProcesses(app).Should().NotBeEmpty();
        }
        detector.FindLockingProcesses(app).Should().BeEmpty();
    }

    [Fact]
    public async Task Journal_PersistsEntriesAcrossInstances()
    {
        var path = Path.Combine(_bed.Root, "journal-test.json");
        var entry = new RelocationEntry(Guid.NewGuid(), @"C:\Apps\Foo", @"D:\PurgeKit\Relocated\C\Apps\Foo",
            RelocationState.Completed, 42, DateTime.UtcNow, DateTime.UtcNow);
        using (var writer = new JsonRelocationJournal(path))
        {
            await writer.SaveAsync(entry);
        }

        using var reader = new JsonRelocationJournal(path);

        (await reader.FindAsync(entry.Id)).Should().Be(entry);
    }

    [Fact]
    public async Task Journal_SaveReplacesEntryWithSameId()
    {
        var entry = new RelocationEntry(Guid.NewGuid(), @"C:\Apps\Foo", @"D:\R\C\Apps\Foo",
            RelocationState.Copying, 42, DateTime.UtcNow, DateTime.UtcNow);
        await _bed.Journal.SaveAsync(entry);

        await _bed.Journal.SaveAsync(entry with { State = RelocationState.Completed });

        (await _bed.SingleEntryAsync()).State.Should().Be(RelocationState.Completed);
    }
}
