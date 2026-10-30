namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Runs classification rules in order; the first rule with an opinion wins, otherwise the app is movable.</summary>
public sealed class AppClassifier : IAppClassifier
{
    private static readonly ClassificationVerdict MovableVerdict = new(AppMoveOption.Movable,
        "Can be moved. PurgeKit checks it again just before moving.");

    private readonly IReadOnlyList<IAppClassificationRule> _rules;

    /// <summary>Creates the classifier with rules in priority order.</summary>
    public AppClassifier(IEnumerable<IAppClassificationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToList();
    }

    /// <inheritdoc />
    public ClassificationVerdict Classify(InstalledApp app, string installPath)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(installPath);
        return _rules.Select(rule => rule.Classify(app, installPath)).FirstOrDefault(verdict => verdict is not null)
            ?? MovableVerdict;
    }
}
