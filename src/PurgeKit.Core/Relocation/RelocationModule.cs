using PurgeKit.Core.Relocation.Infrastructure;
using PurgeKit.Core.Relocation.Rules;

namespace PurgeKit.Core.Relocation;

/// <summary>The relocation module's public entry points, wired together.</summary>
/// <param name="Service">Moves folders and moves them back.</param>
/// <param name="Recovery">Settles interrupted moves; run once at startup.</param>
public sealed record RelocationModule(IRelocationService Service, IRelocationRecovery Recovery)
{
    /// <summary>Default journal location: <c>%LOCALAPPDATA%\PurgeKit\relocations.json</c>.</summary>
    public static string DefaultJournalPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PurgeKit", "relocations.json");

    /// <summary>Wires the module against the real Windows implementations.</summary>
    public static RelocationModule CreateDefault() => Create(new RelocationPlatform
    {
        Journal = new JsonRelocationJournal(DefaultJournalPath),
        Junctions = new NtfsJunctionManager(),
        Copier = new AclPreservingDirectoryCopier(),
        Inspector = new FileSystemDirectoryInspector(),
        Volumes = new DriveVolumeInfoProvider(),
        Locks = new RestartManagerLockDetector(),
        Services = new RegistryServiceRegistry(),
        Elevation = new WindowsElevationStatus(),
        Policy = new WindowsProtectedPathPolicy()
    });

    /// <summary>Wires the module against the given platform.</summary>
    public static RelocationModule Create(RelocationPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        var checkers = new RelocationCheckers(
            new RelocationEligibilityChecker(DefaultRules(platform), platform.Inspector, platform.Journal),
            new RestoreEligibilityChecker(platform.Locks, platform.Volumes, platform.Junctions));
        var fileSystem = new RelocationFileSystem(platform.Copier, platform.Junctions);
        return new RelocationModule(
            new RelocationService(checkers, fileSystem, platform.Journal, platform.Time),
            new RelocationRecovery(platform.Junctions, platform.Journal, platform.Time));
    }

    private static IEligibilityRule[] DefaultRules(RelocationPlatform platform) =>
    [
        new ProtectedPathRule(platform.Policy),
        new AlreadyRelocatedRule(platform.Junctions),
        new LinkedContentRule(),
        new TargetVolumeRule(platform.Volumes),
        new CapacityRule(platform.Volumes),
        new TargetPathRule(),
        new InUseRule(platform.Locks),
        new ServiceRule(platform.Services),
        new ElevationRule(platform.Elevation, platform.Policy)
    ];
}
