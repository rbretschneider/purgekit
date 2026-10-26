using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>Everything an <see cref="IEligibilityRule"/> needs, gathered once per check.</summary>
/// <param name="Request">The relocation being checked.</param>
/// <param name="TargetPath">Where the folder would end up.</param>
/// <param name="Profile">Size and links of the source folder.</param>
/// <param name="ExistingRelocations">Current journal contents.</param>
public sealed record EligibilityContext(
    RelocationRequest Request,
    string TargetPath,
    DirectoryProfile Profile,
    IReadOnlyList<RelocationEntry> ExistingRelocations);
