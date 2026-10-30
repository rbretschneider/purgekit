namespace PurgeKit.Core.Relocation.Apps;

/// <summary>
/// Sends games inside a launcher's library back to the launcher, which updates its own records when it
/// moves them. Libraries are looked up once per rule instance.
/// </summary>
public sealed class LauncherManagedRule : IAppClassificationRule
{
    private readonly Lazy<IReadOnlyList<LauncherLibrary>> _libraries;

    /// <summary>Creates the rule.</summary>
    public LauncherManagedRule(IEnumerable<ILauncherLibraryLocator> locators)
    {
        ArgumentNullException.ThrowIfNull(locators);
        var snapshot = locators.ToList();
        _libraries = new Lazy<IReadOnlyList<LauncherLibrary>>(() => snapshot.SelectMany(l => l.FindLibraries()).ToList());
    }

    /// <inheritdoc />
    public ClassificationVerdict? Classify(InstalledApp app, string installPath)
    {
        var library = _libraries.Value.FirstOrDefault(lib => RelocationPaths.IsSameOrUnder(installPath, lib.RootPath));
        return library is null
            ? null
            : new ClassificationVerdict(AppMoveOption.UseLauncher,
                $"Managed by {library.LauncherName}. Move it from {library.LauncherName}'s storage or install settings.");
    }
}
