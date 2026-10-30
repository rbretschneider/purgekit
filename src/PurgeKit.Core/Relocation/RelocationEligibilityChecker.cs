using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>Gathers the facts about a folder once, then runs every <see cref="IEligibilityRule"/>.</summary>
public sealed class RelocationEligibilityChecker : IEligibilityChecker
{
    private readonly IReadOnlyList<IEligibilityRule> _rules;
    private readonly IDirectoryInspector _inspector;
    private readonly IRelocationJournal _journal;

    /// <summary>Creates the checker.</summary>
    public RelocationEligibilityChecker(
        IEnumerable<IEligibilityRule> rules,
        IDirectoryInspector inspector,
        IRelocationJournal journal)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToList();
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    }

    /// <inheritdoc />
    public async Task<EligibilityReport> CheckAsync(RelocationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var targetPath = RelocationPaths.MapTarget(request.SourcePath, request.TargetRoot);
        if (!Directory.Exists(request.SourcePath))
        {
            return Blocked(request, targetPath, BlockerKind.SourceNotFound, "The folder no longer exists.");
        }

        var existing = await _journal.GetAllAsync(ct).ConfigureAwait(false);
        // Inspection and several rules walk the tree or query the OS synchronously; keep them off the caller's thread.
        return await Task.Run(() => Evaluate(request, targetPath, existing, ct), ct).ConfigureAwait(false);
    }

    private EligibilityReport Evaluate(
        RelocationRequest request,
        string targetPath,
        IReadOnlyList<RelocationEntry> existing,
        CancellationToken ct)
    {
        DirectoryProfile profile;
        try
        {
            profile = _inspector.Inspect(request.SourcePath, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Blocked(request, targetPath, BlockerKind.Inaccessible,
                $"Part of the folder cannot be read ({ex.Message}). Try running PurgeKit as administrator.");
        }

        var context = new EligibilityContext(request, targetPath, profile, existing);
        var blockers = _rules.SelectMany(rule => rule.Evaluate(context)).ToList();
        return new EligibilityReport(request.SourcePath, targetPath, profile.SizeBytes, blockers, request.DisplayName);
    }

    private static EligibilityReport Blocked(RelocationRequest request, string targetPath, BlockerKind kind, string detail) =>
        new(request.SourcePath, targetPath, 0, [new RelocationBlocker(kind, detail)], request.DisplayName);
}
