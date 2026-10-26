using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Rules;

/// <summary>Blocks folders whose files are open in running applications.</summary>
public sealed class InUseRule : IEligibilityRule
{
    private readonly IFileLockDetector _locks;

    /// <summary>Creates the rule.</summary>
    public InUseRule(IFileLockDetector locks)
    {
        _locks = locks ?? throw new ArgumentNullException(nameof(locks));
    }

    /// <inheritdoc />
    public IReadOnlyList<RelocationBlocker> Evaluate(EligibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var apps = _locks.FindLockingProcesses(context.Request.SourcePath);
        return apps.Count > 0
            ? [new RelocationBlocker(BlockerKind.InUse, $"Close {string.Join(", ", apps)} first.")]
            : [];
    }
}
