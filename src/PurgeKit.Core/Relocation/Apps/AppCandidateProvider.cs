namespace PurgeKit.Core.Relocation.Apps;

/// <summary>
/// Builds the Move Apps list: desktop apps whose install folder is a real folder on the system drive,
/// plus Store apps still on the system drive. Already-moved apps are excluded; they appear as moved apps.
/// </summary>
public sealed class AppCandidateProvider : IAppCandidateProvider
{
    private const string StorePublisher = "Microsoft Store";
    private const string StoreReason = "Microsoft Store app. Windows moves it, the same way Settings → Apps → Move does.";
    private const int SizingParallelism = 4;

    private readonly CandidateSources _sources;
    private readonly IAppClassifier _classifier;
    private readonly IFolderSizeEstimator _sizes;
    private readonly IJunctionManager _junctions;

    /// <summary>Creates the provider.</summary>
    public AppCandidateProvider(
        CandidateSources sources,
        IAppClassifier classifier,
        IFolderSizeEstimator sizes,
        IJunctionManager junctions)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _sizes = sizes ?? throw new ArgumentNullException(nameof(sizes));
        _junctions = junctions ?? throw new ArgumentNullException(nameof(junctions));
    }

    /// <inheritdoc />
    public async Task<ListResult<AppCandidate>> GetCandidatesAsync(CancellationToken ct = default)
    {
        var installed = await _sources.Installed.GetAppsAsync(ct).ConfigureAwait(false);
        var packages = await StorePackageListing.TryListAsync(_sources.Store, ct).ConfigureAwait(false);
        // Classification and sizing walk folders synchronously; keep that off the caller's thread.
        var candidates = await Task.Run(() => Build(installed, packages.Items, ct), ct).ConfigureAwait(false);
        return new ListResult<AppCandidate>(candidates, packages.Warning);
    }

    private List<AppCandidate> Build(IReadOnlyList<InstalledApp> installed, IReadOnlyList<StorePackage> packages, CancellationToken ct)
    {
        var desktop = installed
            .Where(app => IsLocalFolderOnSystemDrive(app.InstallPath))
            .GroupBy(app => Path.TrimEndingDirectorySeparator(Path.GetFullPath(app.InstallPath!)), StringComparer.OrdinalIgnoreCase)
            .Select(group => (App: group.First(), Path: group.Key));
        var store = packages.Where(package => IsLocalFolderOnSystemDrive(package.InstallPath));

        var desktopCandidates = desktop.AsParallel().WithDegreeOfParallelism(SizingParallelism).WithCancellation(ct)
            .Select(item => ToCandidate(item.App, item.Path, ct))
            .ToList();
        var storeCandidates = store.AsParallel().WithDegreeOfParallelism(SizingParallelism).WithCancellation(ct)
            .Select(package => ToCandidate(package, ct))
            .ToList();
        return desktopCandidates.Concat(storeCandidates).OrderByDescending(c => c.SizeBytes).ToList();
    }

    private AppCandidate ToCandidate(InstalledApp app, string path, CancellationToken ct)
    {
        var verdict = _classifier.Classify(app, path);
        var size = app.EstimatedSizeBytes > 0 ? app.EstimatedSizeBytes : _sizes.EstimateBytes(path, ct);
        return new AppCandidate(app.Name, app.Publisher, path, size, app.LastUsed, app.LastUsedSource, verdict.Option, verdict.Reason);
    }

    private AppCandidate ToCandidate(StorePackage package, CancellationToken ct) =>
        new(package.Name, StorePublisher, package.InstallPath, _sizes.EstimateBytes(package.InstallPath, ct),
            null, null, AppMoveOption.StoreApp, StoreReason, package.FullName);

    private bool IsLocalFolderOnSystemDrive(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Path.IsPathFullyQualified(path)
        && SystemDrive.Contains(path)
        && Directory.Exists(path)
        && !_junctions.IsLink(path);
}
