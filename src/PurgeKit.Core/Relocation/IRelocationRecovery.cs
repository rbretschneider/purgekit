using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>
/// Brings interrupted relocations (crash, power loss, killed process) to a safe state.
/// Call once at startup, before offering any relocation actions.
/// </summary>
public interface IRelocationRecovery
{
    /// <summary>
    /// Finishes or rolls back every journal entry that is not <see cref="RelocationState.Completed"/>
    /// and returns the entries it acted on.
    /// </summary>
    /// <exception cref="AggregateException">One or more entries could not be recovered; they stay journaled.</exception>
    Task<IReadOnlyList<RelocationEntry>> RecoverAsync(CancellationToken ct = default);
}
