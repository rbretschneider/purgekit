using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Rules;

/// <summary>Blocks Windows, shared component folders, profile roots and PurgeKit itself.</summary>
public sealed class ProtectedPathRule : IEligibilityRule
{
    private readonly IProtectedPathPolicy _policy;

    /// <summary>Creates the rule.</summary>
    public ProtectedPathRule(IProtectedPathPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <inheritdoc />
    public IReadOnlyList<RelocationBlocker> Evaluate(EligibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _policy.IsProtected(context.Request.SourcePath)
            ? [new RelocationBlocker(BlockerKind.ProtectedPath,
                "This is a Windows or shared system folder. Moving it could break Windows or other apps.")]
            : [];
    }
}
