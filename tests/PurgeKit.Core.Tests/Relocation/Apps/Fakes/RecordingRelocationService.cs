namespace PurgeKit.Core.Tests.Relocation.Apps.Fakes;

/// <summary>Records what the app mover asks of the junction relocation service.</summary>
internal sealed class RecordingRelocationService : IRelocationService
{
    public List<RelocationRequest> Relocations { get; } = [];

    public List<Guid> Restores { get; } = [];

    public List<RelocationEntry> Entries { get; } = [];

    public Task<EligibilityReport> CheckEligibilityAsync(RelocationRequest request, CancellationToken ct = default) =>
        Task.FromResult(new EligibilityReport(request.SourcePath, request.TargetRoot, 0, [], request.DisplayName));

    public Task<RelocationEntry> RelocateAsync(RelocationRequest request, IProgress<RelocationProgress>? progress = null, CancellationToken ct = default)
    {
        Relocations.Add(request);
        return Task.FromResult(new RelocationEntry(Guid.NewGuid(), request.SourcePath, request.TargetRoot,
            RelocationState.Completed, 0, DateTime.UtcNow, DateTime.UtcNow, request.DisplayName));
    }

    public Task<EligibilityReport> CheckRestoreAsync(Guid relocationId, CancellationToken ct = default) =>
        Task.FromResult(new EligibilityReport("", "", 0, []));

    public Task RestoreAsync(Guid relocationId, IProgress<RelocationProgress>? progress = null, CancellationToken ct = default)
    {
        Restores.Add(relocationId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RelocationEntry>> GetRelocationsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RelocationEntry>>(Entries);
}
