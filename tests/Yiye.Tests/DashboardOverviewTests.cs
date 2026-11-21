using Yiye.Models;
using Yiye.Presentation;

namespace Yiye.Tests;

public sealed class DashboardOverviewTests
{
    [Fact]
    public void CompletedWorks_CountEachWorkOnceAcrossRepeatedCompletions()
    {
        var book = new DashboardShowcaseItem { WorkId = "book", Kind = "book", CompletionCount = 5 };
        var overview = DashboardOverview.FromCompletedWorks([
            book, book,
            new() { WorkId = "film", Kind = "screen", CompletionCount = 2 },
            new() { WorkId = "saved-book", Kind = "book", CompletionCount = 0 }
        ]);

        Assert.Equal(1, overview.BookCount);
        Assert.Equal(1, overview.ScreenCount);
        Assert.Equal(2, overview.CompletedWorkCount);
        Assert.Equal(0.5, overview.BookShare);
        Assert.Equal(0.5, overview.ScreenShare);
    }

    [Fact]
    public void EmptyCollection_HasZeroCountsAndShares()
    {
        var overview = DashboardOverview.FromCompletedWorks([]);

        Assert.Equal(0, overview.CompletedWorkCount);
        Assert.Equal(0, overview.BookShare);
        Assert.Equal(0, overview.ScreenShare);
    }

    [Fact]
    public void CategoryShares_ReflectCompletedWorkCounts()
    {
        var overview = DashboardOverview.FromCompletedWorks([
            new() { WorkId = "book-1", Kind = "book", CompletionCount = 1 },
            new() { WorkId = "book-2", Kind = "book", CompletionCount = 3 },
            new() { WorkId = "film", Kind = "screen", CompletionCount = 1 }
        ]);

        Assert.Equal(2.0 / 3, overview.BookShare);
        Assert.Equal(1.0 / 3, overview.ScreenShare);
        Assert.Equal(1, overview.BookShare + overview.ScreenShare);
    }
}
