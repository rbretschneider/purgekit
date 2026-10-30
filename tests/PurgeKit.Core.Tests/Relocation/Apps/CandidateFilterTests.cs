using PurgeKit.Core.Relocation.Apps;

namespace PurgeKit.Core.Tests.Relocation.Apps;

public sealed class CandidateFilterTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly CandidateFilter Default = new(MinIdleDays: 90, MinSizeBytes: 100, SearchText: "", IncludeUnmovable: false);

    [Fact]
    public void AppUsedRecently_IsHidden()
    {
        Default.Matches(Candidate(lastUsed: Now.AddDays(-10)), Now).Should().BeFalse();
    }

    [Fact]
    public void AppIdleLongEnough_IsShown()
    {
        Default.Matches(Candidate(lastUsed: Now.AddDays(-120)), Now).Should().BeTrue();
    }

    [Fact]
    public void AppWithUnknownLastUse_IsShown()
    {
        Default.Matches(Candidate(lastUsed: null), Now).Should().BeTrue();
    }

    [Fact]
    public void SmallApp_IsHidden()
    {
        Default.Matches(Candidate(size: 99), Now).Should().BeFalse();
    }

    [Fact]
    public void UnmovableApp_IsHiddenUnlessAskedFor()
    {
        var blocked = Candidate(option: AppMoveOption.Blocked);

        Default.Matches(blocked, Now).Should().BeFalse();
        (Default with { IncludeUnmovable = true }).Matches(blocked, Now).Should().BeTrue();
    }

    [Fact]
    public void Search_MatchesPublisherCaseInsensitively()
    {
        (Default with { SearchText = "adobe" }).Matches(Candidate(), Now).Should().BeTrue();
        (Default with { SearchText = "autodesk" }).Matches(Candidate(), Now).Should().BeFalse();
    }

    private static AppCandidate Candidate(DateTime? lastUsed = null, long size = 1000, AppMoveOption option = AppMoveOption.Movable) =>
        new("Photoshop", "Adobe Inc.", @"C:\Program Files\Adobe", size, lastUsed, null, option, "");
}
