namespace PurgeKit.Core.Relocation;

/// <summary>The pre-flight checks the relocation service runs before moving anything.</summary>
/// <param name="Relocate">Checks for moving a folder away.</param>
/// <param name="Restore">Checks for moving it back.</param>
public sealed record RelocationCheckers(IEligibilityChecker Relocate, IRestoreEligibilityChecker Restore);
