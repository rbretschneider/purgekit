using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>
/// Settles interrupted relocations at startup. A move that reached <see cref="RelocationState.Swapped"/>
/// with a working junction is finished; anything earlier is rolled back. An interrupted restore is
/// finished if the real folder is already back, otherwise rolled back.
/// </summary>
public sealed class RelocationRecovery : IRelocationRecovery
{
    private readonly IRelocationJournal _journal;
    private readonly RelocationSteps _steps;

    /// <summary>Creates the recovery handler.</summary>
    public RelocationRecovery(IJunctionManager junctions, IRelocationJournal journal, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(junctions);
        ArgumentNullException.ThrowIfNull(time);
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _steps = new RelocationSteps(junctions, journal, time);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RelocationEntry>> RecoverAsync(CancellationToken ct = default)
    {
        var entries = await _journal.GetAllAsync(ct).ConfigureAwait(false);
        var interrupted = entries.Where(e => e.State != RelocationState.Completed).ToList();
        var failures = new List<Exception>();
        foreach (var entry in interrupted)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await RecoverEntryAsync(entry).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                failures.Add(new IOException($"Could not recover the move of '{entry.SourcePath}' ({entry.State}).", ex));
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("Some interrupted moves could not be recovered.", failures);
        }
        return interrupted;
    }

    private Task RecoverEntryAsync(RelocationEntry entry) => entry.State switch
    {
        RelocationState.Swapped when _steps.IsJunctionToTarget(entry) => _steps.CompleteRelocationAsync(entry),
        RelocationState.Copying or RelocationState.Verified or RelocationState.Swapped => _steps.RollBackRelocationAsync(entry),
        RelocationState.RestoreVerified when _steps.IsRealDirectory(entry.SourcePath) => _steps.CompleteRestoreAsync(entry),
        RelocationState.Restoring or RelocationState.RestoreVerified => _steps.RollBackRestoreAsync(entry),
        _ => Task.CompletedTask
    };
}
