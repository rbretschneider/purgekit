using PurgeKit.Core.Models;
using PurgeKit.Core.Relocation.Apps;

namespace PurgeKit.UI.ViewModels;

/// <summary>One row of the Move Apps list.</summary>
public sealed class AppCandidateItemVm
{
    /// <summary>Wraps <paramref name="candidate"/>.</summary>
    public AppCandidateItemVm(AppCandidate candidate)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
    }

    /// <summary>The underlying candidate.</summary>
    public AppCandidate Candidate { get; }

    /// <summary>Live status of a move started from this row.</summary>
    public OperationStatusVm Operation { get; } = new();

    public string Name => Candidate.Name;

    public string Publisher => Candidate.Publisher;

    public string InstallPath => Candidate.InstallPath;

    public string SizeText => ScanResult.FormatSize(Candidate.SizeBytes);

    public long SizeBytes => Candidate.SizeBytes;

    public string LastUsedText => Candidate.LastUsed?.ToString("yyyy-MM-dd") ?? "Unknown";

    public string LastUsedTooltip => Candidate.LastUsedSource is null
        ? "PurgeKit could not tell when this was last used."
        : $"Date sourced from {Candidate.LastUsedSource}";

    public DateTime LastUsedSortKey => Candidate.LastUsed ?? DateTime.MinValue;

    public AppMoveOption Option => Candidate.Option;

    public string Reason => Candidate.Reason;

    public bool CanMove => Candidate.CanMove;
}
