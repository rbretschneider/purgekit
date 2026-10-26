using System.Text.Json;
using System.Text.Json.Serialization;
using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Infrastructure;

/// <summary>
/// Journal stored as a JSON file. Writes go to a temp file that atomically replaces the journal, so a
/// crash mid-write never leaves it half-written. Safe for concurrent use within one process.
/// </summary>
public sealed class JsonRelocationJournal : IRelocationJournal, IDisposable
{
    private const string TempSuffix = ".tmp";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates a journal backed by <paramref name="filePath"/>; the file is created on first write.</summary>
    public JsonRelocationJournal(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException($"'{filePath}' must be an absolute path.", nameof(filePath));
        }
        _filePath = filePath;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RelocationEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await ReadAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RelocationEntry?> FindAsync(Guid id, CancellationToken ct = default)
    {
        var entries = await GetAllAsync(ct).ConfigureAwait(false);
        return entries.FirstOrDefault(entry => entry.Id == id);
    }

    /// <inheritdoc />
    public Task SaveAsync(RelocationEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return MutateAsync(entries =>
        {
            entries.RemoveAll(existing => existing.Id == entry.Id);
            entries.Add(entry);
        }, ct);
    }

    /// <inheritdoc />
    public Task RemoveAsync(Guid id, CancellationToken ct = default) =>
        MutateAsync(entries => entries.RemoveAll(entry => entry.Id == id), ct);

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private async Task MutateAsync(Action<List<RelocationEntry>> change, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entries = await ReadAsync(ct).ConfigureAwait(false);
            change(entries);
            await WriteAsync(entries, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<RelocationEntry>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }
        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<RelocationEntry>>(stream, SerializerOptions, ct).ConfigureAwait(false)
            ?? [];
    }

    private async Task WriteAsync(List<RelocationEntry> entries, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var tempPath = _filePath + TempSuffix;
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, entries, SerializerOptions, ct).ConfigureAwait(false);
        }
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
