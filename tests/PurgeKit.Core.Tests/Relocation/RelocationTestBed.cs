using PurgeKit.Core.Relocation.Infrastructure;
using PurgeKit.Core.Tests.Relocation.Fakes;

namespace PurgeKit.Core.Tests.Relocation;

/// <summary>
/// A throwaway sandbox with a fake "source drive" and "target drive" (two folders that the fake volume
/// provider reports as separate fixed NTFS volumes). Real junctions, copier, inspector and journal are
/// used; only OS queries that would depend on the machine are faked.
/// </summary>
internal sealed class RelocationTestBed : IDisposable
{
    public const string ExecutableName = "app.exe";
    public const string SettingsFile = @"data\settings.json";
    public const string ReadOnlyFile = @"data\readme.txt";
    public const string SettingsContent = "{\"theme\":\"dark\"}";

    private static readonly DateTime FixedWriteTimeUtc = new(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);

    private readonly JsonRelocationJournal _journal;

    public RelocationTestBed()
    {
        Root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "purgekit-tests", Guid.NewGuid().ToString("N"))).FullName;
        SourceDrive = Directory.CreateDirectory(Path.Combine(Root, "source-drive")).FullName;
        TargetDrive = Directory.CreateDirectory(Path.Combine(Root, "target-drive")).FullName;
        TargetRoot = Path.Combine(TargetDrive, "Relocated");
        _journal = new JsonRelocationJournal(Path.Combine(Root, "relocations.json"));
        Volumes.SetVolume(SourceDrive);
        Volumes.SetVolume(TargetDrive);
    }

    public string Root { get; }

    public string SourceDrive { get; }

    public string TargetDrive { get; }

    public string TargetRoot { get; }

    public IRelocationJournal Journal => _journal;

    public NtfsJunctionManager Junctions { get; } = new();

    public FakeVolumeInfoProvider Volumes { get; } = new();

    public FakeFileLockDetector Locks { get; } = new();

    public FakeServiceRegistry Services { get; } = new();

    public FakeElevationStatus Elevation { get; } = new();

    public FakeProtectedPathPolicy Policy { get; } = new();

    public IDirectoryCopier Copier { get; set; } = new AclPreservingDirectoryCopier();

    public IRelocationService CreateService() => RelocationModule.Create(Platform()).Service;

    public IRelocationRecovery CreateRecovery() => RelocationModule.Create(Platform()).Recovery;

    /// <summary>Creates an app folder with an executable, a settings file and a read-only file.</summary>
    public string CreateApp(string name = "App")
    {
        var app = Directory.CreateDirectory(Path.Combine(SourceDrive, name)).FullName;
        Directory.CreateDirectory(Path.Combine(app, "data"));
        File.WriteAllBytes(Path.Combine(app, ExecutableName), Enumerable.Range(0, 4096).Select(i => (byte)i).ToArray());
        File.WriteAllText(Path.Combine(app, SettingsFile), SettingsContent);
        var readOnly = Path.Combine(app, ReadOnlyFile);
        File.WriteAllText(readOnly, "do not edit");
        File.SetLastWriteTimeUtc(readOnly, FixedWriteTimeUtc);
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);
        return app;
    }

    public RelocationRequest RequestFor(string sourcePath) => new(sourcePath, TargetRoot);

    public string TargetOf(string sourcePath) => RelocationPaths.MapTarget(sourcePath, TargetRoot);

    public Task<RelocationEntry> RelocateAsync(string sourcePath) => CreateService().RelocateAsync(RequestFor(sourcePath));

    public async Task<IReadOnlyList<BlockerKind>> BlockersForAsync(string sourcePath)
    {
        var report = await CreateService().CheckEligibilityAsync(RequestFor(sourcePath));
        return report.Blockers.Select(b => b.Kind).ToList();
    }

    public async Task<RelocationEntry> SingleEntryAsync() => (await Journal.GetAllAsync()).Should().ContainSingle().Subject;

    public Task SaveEntryAsync(string sourcePath, RelocationState state) =>
        Journal.SaveAsync(new RelocationEntry(Guid.NewGuid(), sourcePath, TargetOf(sourcePath), state, 0, DateTime.UtcNow, DateTime.UtcNow));

    public static DateTime ExpectedReadOnlyWriteTimeUtc => FixedWriteTimeUtc;

    public void Dispose()
    {
        _journal.Dispose();
        DirectoryDeletion.DeleteTree(Root);
    }

    private RelocationPlatform Platform() => new()
    {
        Journal = Journal,
        Junctions = Junctions,
        Copier = Copier,
        Inspector = new FileSystemDirectoryInspector(),
        Volumes = Volumes,
        Locks = Locks,
        Services = Services,
        Elevation = Elevation,
        Policy = Policy
    };
}
