using Yiye.Models;

namespace Yiye.Tests;

public sealed class LibraryMonthTests
{
    [Fact]
    public void GroupsByLatestRecordMonthAcrossYearsAndKeepsUndatedWorksSeparate()
    {
        var september = Work("recent", new DateOnly(2026, 9, 26));
        var sameMonth = Work("earlier", new DateOnly(2026, 9, 1));
        var previousYear = Work("previous", new DateOnly(2025, 9, 30));
        var undated = Work("undated", null);
        var groups = LibraryMonth.Group([previousYear, sameMonth, undated, september]);

        Assert.Equal(["2026-09", "2025-09", "unrecorded"], groups.Select(group => group.Key));
        Assert.Equal([september.Id, sameMonth.Id], groups[0].Works.Select(work => work.Id));
        Assert.Equal("尚无记录", groups[2].Label);
        Assert.Same(undated, Assert.Single(groups[2].Works));
        Assert.Equal(4, groups.Sum(group => group.Works.Count));
    }

    [Fact]
    public void PreviewUsesAtMostThreeMostRecentWorksWithNewestInFront()
    {
        var works = Enumerable.Range(1, 5).Select(day => Work($"work {day}", new DateOnly(2026, 9, day))).ToArray();
        var month = Assert.Single(LibraryMonth.Group(works));
        Assert.Equal(5, month.Works.Count);
        Assert.Equal(3, month.PreviewCards.Count);
        Assert.Equal([3, 4, 5], month.PreviewCards.Select(card => card.Work.LatestActivityOn!.Value.Day));
        Assert.Equal(3, month.PreviewCards[^1].Layer);
        Assert.Equal("5 部作品", month.CountLabel);
        Assert.Empty(LibraryMonth.Group([]));
    }

    [Fact]
    public void ANewRecordMovesAWorkToItsNewMonthOnly()
    {
        var updated = new MediaWork { Id = "same-work", Title = "updated", Kind = "book",
            CreatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            LatestActivityOn = new DateOnly(2026, 10, 1) };
        var month = Assert.Single(LibraryMonth.Group([updated]));
        Assert.Equal("2026-10", month.Key);
        Assert.Equal(updated.Id, Assert.Single(month.Works).Id);
        Assert.Single(month.PreviewCards);
    }

    private static MediaWork Work(string title, DateOnly? date) =>
        new() { Title = title, Kind = "book", LatestActivityOn = date };
}
