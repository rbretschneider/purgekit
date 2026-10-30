using PurgeKit.Core.Models;
using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Apps;

/// <summary>
/// Moves desktop apps with the junction-based <see cref="IRelocationService"/> and Store apps with Windows'
/// own app mover, so callers treat every movable app the same way.
/// </summary>
public sealed class AppMover : IAppMover
{
    private readonly IRelocationService _relocation;
    private readonly IStorePackageCatalog _store;
    private readonly IVolumeInfoProvider _volumes;

    /// <summary>Creates the mover.</summary>
    public AppMover(IRelocationService relocation, IStorePackageCatalog store, IVolumeInfoProvider volumes)
    {
        _relocation = relocation ?? throw new ArgumentNullException(nameof(relocation));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _volumes = volumes ?? throw new ArgumentNullException(nameof(volumes));
    }

    /// <inheritdoc />
    public Task<EligibilityReport> CheckMoveAsync(AppCandidate candidate, TargetDrive drive, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(drive);
        return candidate.Option switch
        {
            AppMoveOption.Movable => _relocation.CheckEligibilityAsync(RequestFor(candidate, drive), ct),
            AppMoveOption.StoreApp => Task.FromResult(StoreSpaceReport(candidate.InstallPath, drive.RootPath, candidate.SizeBytes, drive.FreeBytes)),
            _ => throw NotMovable(candidate)
        };
    }

    /// <inheritdoc />
    public async Task MoveAsync(AppCandidate candidate, TargetDrive drive, IProgress<RelocationProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(drive);
        switch (candidate.Option)
        {
            case AppMoveOption.Movable:
                await _relocation.RelocateAsync(RequestFor(candidate, drive), progress, ct).ConfigureAwait(false);
                break;
            case AppMoveOption.StoreApp:
                await MoveStorePackageAsync(candidate.PackageFullName!, drive.RootPath, candidate.SizeBytes, progress, ct).ConfigureAwait(false);
                break;
            default:
                throw NotMovable(candidate);
        }
    }

    /// <inheritdoc />
    public async Task<ListResult<MovedApp>> GetMovedAppsAsync(CancellationToken ct = default)
    {
        var relocations = await _relocation.GetRelocationsAsync(ct).ConfigureAwait(false);
        var packages = await StorePackageListing.TryListAsync(_store, ct).ConfigureAwait(false);
        var moved = relocations.Select(ToMovedApp)
            .Concat(packages.Items.Where(p => !SystemDrive.Contains(p.InstallPath)).Select(ToMovedApp))
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new ListResult<MovedApp>(moved, packages.Warning);
    }

    /// <inheritdoc />
    public Task<EligibilityReport> CheckMoveBackAsync(MovedApp app, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.Kind == MovedAppKind.Junction)
        {
            return _relocation.CheckRestoreAsync(RequireRelocationId(app), ct);
        }
        var free = _volumes.GetVolume(SystemDrive.Root)?.AvailableFreeBytes ?? 0;
        return Task.FromResult(StoreSpaceReport(app.CurrentPath, SystemDrive.Root, app.SizeBytes, free));
    }

    /// <inheritdoc />
    public Task MoveBackAsync(MovedApp app, IProgress<RelocationProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Kind == MovedAppKind.Junction
            ? _relocation.RestoreAsync(RequireRelocationId(app), progress, ct)
            : MoveStorePackageAsync(app.PackageFullName!, SystemDrive.Root, app.SizeBytes, progress, ct);
    }

    private async Task MoveStorePackageAsync(string fullName, string driveRoot, long sizeBytes, IProgress<RelocationProgress>? progress, CancellationToken ct)
    {
        // Windows reports no byte progress for package moves; total 0 tells the UI to show an indeterminate bar.
        progress?.Report(new RelocationProgress(RelocationStage.Copying, 0, 0));
        await _store.MovePackageAsync(fullName, driveRoot, ct).ConfigureAwait(false);
        progress?.Report(new RelocationProgress(RelocationStage.Cleaning, sizeBytes, sizeBytes));
    }

    private static RelocationRequest RequestFor(AppCandidate candidate, TargetDrive drive) =>
        new(candidate.InstallPath, RelocationPaths.DefaultTargetRoot(drive.RootPath), candidate.Name);

    private static EligibilityReport StoreSpaceReport(string fromPath, string toRoot, long sizeBytes, long freeBytes)
    {
        var required = RelocationCapacity.RequiredFreeBytes(sizeBytes);
        IReadOnlyList<RelocationBlocker> blockers = freeBytes >= required
            ? []
            : [new RelocationBlocker(BlockerKind.InsufficientSpace,
                $"{toRoot} has {ScanResult.FormatSize(freeBytes)} free; this needs {ScanResult.FormatSize(required)}.")];
        return new EligibilityReport(fromPath, toRoot, sizeBytes, blockers);
    }

    private static MovedApp ToMovedApp(RelocationEntry entry) => new(
        entry.DisplayName ?? Path.GetFileName(entry.SourcePath), MovedAppKind.Junction, entry.SourcePath, entry.TargetPath,
        entry.SizeBytes, entry.UpdatedUtc, entry.State != RelocationState.Completed, RelocationId: entry.Id);

    private static MovedApp ToMovedApp(StorePackage package) => new(
        package.Name, MovedAppKind.StorePackage, SystemDrive.Root, package.InstallPath, 0, null, false,
        PackageFullName: package.FullName);

    private static Guid RequireRelocationId(MovedApp app) =>
        app.RelocationId ?? throw new ArgumentException("A junction move must carry its relocation id.", nameof(app));

    private static InvalidOperationException NotMovable(AppCandidate candidate) =>
        new($"{candidate.Name} cannot be moved by PurgeKit: {candidate.Reason}");
}
