using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Rules;

/// <summary>Requires a free target path, no leftovers from earlier attempts, and no target inside the source.</summary>
public sealed class TargetPathRule : IEligibilityRule
{
    /// <inheritdoc />
    public IReadOnlyList<RelocationBlocker> Evaluate(EligibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var blockers = new List<RelocationBlocker>();
        if (Path.Exists(context.TargetPath))
        {
            blockers.Add(new RelocationBlocker(BlockerKind.TargetAlreadyExists,
                $"{context.TargetPath} already exists. Remove or rename it first."));
        }
        if (HasLeftovers(context.Request.SourcePath))
        {
            blockers.Add(new RelocationBlocker(BlockerKind.LeftoverArtifacts,
                "Files from an earlier interrupted move sit next to this folder. Restart PurgeKit to clean them up."));
        }
        if (RelocationPaths.IsSameOrUnder(context.Request.TargetRoot, context.Request.SourcePath))
        {
            blockers.Add(new RelocationBlocker(BlockerKind.TargetInsideSource,
                "The target folder is inside the folder being moved."));
        }
        return blockers;
    }

    private static bool HasLeftovers(string sourcePath) =>
        Path.Exists(RelocationPaths.PendingPath(sourcePath)) || Path.Exists(RelocationPaths.RestoringPath(sourcePath));
}
