using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Rules;

/// <summary>
/// Blocks folders that contain junctions, symlinks or other reparse points. Copying them faithfully
/// (as links rather than their contents) is not supported yet, so they are refused rather than flattened.
/// </summary>
public sealed class LinkedContentRule : IEligibilityRule
{
    private const int LinksShown = 3;

    /// <inheritdoc />
    public IReadOnlyList<RelocationBlocker> Evaluate(EligibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var links = context.Profile.LinkPaths;
        return links.Count > 0
            ? [new RelocationBlocker(BlockerKind.ContainsLinks,
                $"The folder contains {links.Count} link(s), such as {string.Join(", ", links.Take(LinksShown))}. "
                + "Folders with links inside cannot be moved yet.")]
            : [];
    }
}
