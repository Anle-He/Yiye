using Yiye.Models;
using Yiye.Presentation;

namespace Yiye.Tests;

public sealed class DashboardRankingCardTests
{
    [Fact]
    public void WorkRanking_UsesItsOwnScaleAndKeepsScoreUnchanged()
    {
        var card = new ShowcaseCard(new DashboardShowcaseItem { AggregateRank = 1.95 }, position: 1);

        Assert.Equal(1, card.Position);
        Assert.Equal("01", card.PositionLabel);
        Assert.True(card.IsLead);
        Assert.Equal(0.5, card.ScoreRatio);
        Assert.Equal(1.95, card.AggregateRank);
        Assert.False(new ShowcaseCard(new DashboardShowcaseItem(), position: 2).IsLead);
        Assert.Equal(0, new ShowcaseCard(new DashboardShowcaseItem()).ScoreRatio);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(8, 1)]
    public void WorkScoreBars_StayWithinTheirTrackWithoutChangingScores(double score, double expectedRatio)
    {
        var work = new ShowcaseCard(new DashboardShowcaseItem { AggregateRank = score });

        Assert.Equal(expectedRatio, work.ScoreRatio);
        Assert.Equal(score, work.AggregateRank);
    }

    [Fact]
    public void AuthorRanking_UsesFivePointScaleAndExistingRatedBookCount()
    {
        var card = new AuthorCard(new DashboardAuthorRank { Author = "作者", Score = 4.5, WorkCount = 2 });

        Assert.Equal(4.5, card.Score);
        Assert.Equal("已评 2 本", card.WorkCountLabel);
        Assert.Equal("作者\n4.5 / 5.0 · 已评 2 本", card.Summary);
    }

    [Fact]
    public void AuthorCloud_UsesFirstEightAuthorsAndSizesOnlyTheirScores()
    {
        var authors = Enumerable.Range(1, 10).Select(index => new DashboardAuthorRank
        { Position = index, Author = $"作者 {index}", Score = 5 - index * 0.3 }).ToArray();

        var cards = AuthorCard.CreateCloud(authors);

        Assert.Equal(authors.Take(8), cards.Select(card => card.Value));
        Assert.Equal(30, cards[0].FontSize);
        Assert.Equal(18, cards[^1].FontSize);
        Assert.All(cards, card => Assert.Equal(card.Value.Score, card.Score));
        Assert.All(cards, card => Assert.Equal(card.FontSize * 0.9, card.CompactFontSize));
        for (var index = 1; index < cards.Count; index++)
            Assert.True(cards[index - 1].FontSize > cards[index].FontSize);
        Assert.Equal(authors.Take(3), AuthorCard.CreateCloud(authors, limit: 3).Select(card => card.Value));
    }

    [Fact]
    public void AuthorCloud_EqualScoresHaveEqualTypeSizeAndPreserveInputOrder()
    {
        DashboardAuthorRank[] authors = [
            new() { Author = "A", Score = 4.2 },
            new() { Author = "B", Score = 4.2 },
            new() { Author = "C", Score = 3.0 }
        ];
        var cards = AuthorCard.CreateCloud(authors);
        Assert.Equal(authors, cards.Select(card => card.Value));
        Assert.Equal(cards[0].FontSize, cards[1].FontSize);
        Assert.All(AuthorCard.CreateCloud(authors.Take(2)), card => Assert.Equal(24, card.FontSize));
        Assert.Equal(24, Assert.Single(AuthorCard.CreateCloud(authors.Take(1))).FontSize);
        Assert.Empty(AuthorCard.CreateCloud([]));
    }

    [Theory]
    [InlineData(1, "鲁迅", false)]
    [InlineData(3, "林晓光", true)]
    [InlineData(6, "鲁迅", true)]
    [InlineData(6, "村上春树", true)]
    [InlineData(3, "Jane Austen", false)]
    [InlineData(3, "加西亚·马尔克斯", false)]
    [InlineData(2, "村上春树", false)]
    public void AuthorCloud_UsesUprightVerticalTextForSelectedShortChineseNames(int position, string author, bool vertical)
    {
        var card = new AuthorCard(new DashboardAuthorRank
        { Position = position, Author = author, Score = 4.2, WorkCount = 2 });

        Assert.Equal(vertical, card.IsVertical);
        Assert.Equal(vertical ? string.Join("\n", author.ToCharArray()) : author, card.CloudLabel);
        Assert.Equal($"{author}\n4.2 / 5.0 · 已评 2 本", card.Summary);
    }
}
