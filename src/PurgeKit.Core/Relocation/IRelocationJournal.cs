using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>Durable record of every relocation and its state.</summary>
public interface IRelocationJournal
{
    /// <summary>Every recorded relocation.</summary>
    Task<IReadOnlyList<RelocationEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>The relocation with <paramref name="id"/>, or null if there is none.</summary>
    Task<RelocationEntry?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Adds the entry, or replaces the existing entry with the same id.</summary>
    Task SaveAsync(RelocationEntry entry, CancellationToken ct = default);

    /// <summary>Removes the entry with <paramref name="id"/> if present.</summary>
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
