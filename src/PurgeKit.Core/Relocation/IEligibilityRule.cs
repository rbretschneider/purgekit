using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>One independent reason a relocation might be unsafe. Add a rule to add a check.</summary>
public interface IEligibilityRule
{
    /// <summary>Blockers this rule finds for the context; empty when it has no objection.</summary>
    IReadOnlyList<RelocationBlocker> Evaluate(EligibilityContext context);
}
