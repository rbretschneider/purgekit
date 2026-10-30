using PurgeKit.Core.Relocation.Apps.Infrastructure;
using PurgeKit.Core.Relocation.Infrastructure;

namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Everything the Move Apps screen needs, wired together.</summary>
/// <param name="Candidates">Finds and classifies apps on the system drive.</param>
/// <param name="Mover">Moves apps and moves them back.</param>
/// <param name="Drives">Lists drives that can receive apps.</param>
/// <param name="Recovery">Settles interrupted moves; run once at startup.</param>
public sealed record AppRelocationServices(
    IAppCandidateProvider Candidates,
    IAppMover Mover,
    ITargetDriveCatalog Drives,
    IRelocationRecovery Recovery)
{
    /// <summary>Wires the screen against the real Windows implementations.</summary>
    public static AppRelocationServices CreateDefault()
    {
        var junctions = new NtfsJunctionManager();
        var policy = new WindowsProtectedPathPolicy();
        var relocation = RelocationModule.CreateDefault();
        var store = new PowerShellStorePackageCatalog(new PowerShellRunner());
        var classifier = new AppClassifier(
        [
            new ProtectedLocationRule(policy),
            new LauncherManagedRule([new SteamLibraryLocator(), new EpicLibraryLocator(), new GogLibraryLocator()]),
            new DenylistRule()
        ]);

        return new AppRelocationServices(
            new AppCandidateProvider(new CandidateSources(new RegistryInstalledAppSource(), store), classifier, new FolderSizeEstimator(), junctions),
            new AppMover(relocation.Service, store, new DriveVolumeInfoProvider()),
            new FixedDriveCatalog(),
            relocation.Recovery);
    }
}
