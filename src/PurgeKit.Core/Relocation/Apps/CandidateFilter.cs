namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Which candidates the Move Apps list shows.</summary>
/// <param name="MinIdleDays">Only apps not used for at least this many days; null for any. Apps with an unknown last-used date always pass.</param>
/// <param name="MinSizeBytes">Only apps at least this big.</param>
/// <param name="SearchText">Case-insensitive match on name or publisher; empty for all.</param>
/// <param name="IncludeUnmovable">Also show apps that are blocked or must be moved by a launcher.</param>
public sealed record CandidateFilter(int? MinIdleDays, long MinSizeBytes, string SearchText, bool IncludeUnmovable)
{
    /// <summary>True when <paramref name="candidate"/> should be listed at time <paramref name="nowUtc"/>.</summary>
    public bool Matches(AppCandidate candidate, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return (IncludeUnmovable || candidate.CanMove)
            && candidate.SizeBytes >= MinSizeBytes
            && IsIdleLongEnough(candidate, nowUtc)
            && MatchesSearch(candidate);
    }

    private bool IsIdleLongEnough(AppCandidate candidate, DateTime nowUtc) =>
        MinIdleDays is null
        || candidate.LastUsed is null
        || candidate.LastUsed.Value.ToUniversalTime() <= nowUtc.AddDays(-MinIdleDays.Value);

    private bool MatchesSearch(AppCandidate candidate) =>
        string.IsNullOrWhiteSpace(SearchText)
        || candidate.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
        || candidate.Publisher.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);
}
