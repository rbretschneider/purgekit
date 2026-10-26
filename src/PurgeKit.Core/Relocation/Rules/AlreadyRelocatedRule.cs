using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Rules;

/// <summary>Blocks folders that are already links or already journaled.</summary>
public sealed class AlreadyRelocatedRule : IEligibilityRule
{
    private readonly IJunctionManager _junctions;

    /// <summary>Creates the rule.</summary>
    public AlreadyRelocatedRule(IJunctionManager junctions)
    {
        _junctions = junctions ?? throw new ArgumentNullException(nameof(junctions));
    }

    /// <inheritdoc />
    public IReadOnlyList<RelocationBlocker> Evaluate(EligibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return IsAlreadyRelocated(context)
            ? [new RelocationBlocker(BlockerKind.AlreadyRelocated,
                "This folder has already been moved, or is a link to somewhere else.")]
            : [];
    }

    private bool IsAlreadyRelocated(EligibilityContext context)
    {
        var source = context.Request.SourcePath;
        return _junctions.IsLink(source)
            || context.ExistingRelocations.Any(entry => RelocationPaths.AreSame(entry.SourcePath, source));
    }
}
