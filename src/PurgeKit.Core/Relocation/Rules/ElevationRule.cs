using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Rules;

/// <summary>Blocks admin-owned folders when PurgeKit is not running elevated.</summary>
public sealed class ElevationRule : IEligibilityRule
{
    private readonly IElevationStatus _elevation;
    private readonly IProtectedPathPolicy _policy;

    /// <summary>Creates the rule.</summary>
    public ElevationRule(IElevationStatus elevation, IProtectedPathPolicy policy)
    {
        _elevation = elevation ?? throw new ArgumentNullException(nameof(elevation));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <inheritdoc />
    public IReadOnlyList<RelocationBlocker> Evaluate(EligibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return !_elevation.IsElevated && _policy.RequiresElevation(context.Request.SourcePath)
            ? [new RelocationBlocker(BlockerKind.RequiresElevation,
                "Moving apps out of Program Files needs administrator rights. Restart PurgeKit as administrator.")]
            : [];
    }
}
