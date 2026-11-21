using Yiye.Models;

namespace Yiye.Tests;

public sealed class DashboardAuthorLimitTests
{
    [Fact]
    public async Task AuthorLimit_ReturnsRequestedLeadersWithStableTieOrdering()
    {
        await using var context = await TempDatabase.CreateAsync();
        async Task AddRatedAuthor(string author, int dimensionScore, int ratings = 1)
        {
            var work = new MediaWork { Title = author, Author = author, Kind = "book" };
            await context.Repository.AddWorkAsync(work);
            for (var index = 0; index < ratings; index++)
                await context.Repository.AddExperienceAsync(new MediaExperience
                {
                    WorkId = work.Id, CompletedOn = new DateOnly(2026, 10, index + 1),
                    Allure = 3, Immersion = dimensionScore, Rationality = dimensionScore, Illumination = dimensionScore
                });
        }

        // Insert in an order different from score, rating-count and author ordering.
        for (var index = 19; index >= 0; index--)
            await AddRatedAuthor($"Other{index:00}", 3);
        await AddRatedAuthor("BetaTie", 4);
        await AddRatedAuthor("AlphaTie", 4);
        await AddRatedAuthor("ZRepeated", 4, ratings: 2);
        await AddRatedAuthor("ZuluHigh", 5);

        var expected = new[] { "ZuluHigh", "ZRepeated", "AlphaTie", "BetaTie" }
            .Concat(Enumerable.Range(0, 20).Select(index => $"Other{index:00}")).ToArray();
        var defaults = (await context.Repository.GetDashboardShowcaseAsync()).TopAuthors;
        Assert.Equal(expected.Take(8), defaults.Select(author => author.Author));
        Assert.Equal(Enumerable.Range(1, 8), defaults.Select(author => author.Position));
        Assert.Equal(5.0, defaults[0].Score);
        Assert.Equal(defaults[1].Score, defaults[2].Score);
        Assert.Equal(2, defaults[1].RatingCount);

        var smaller = (await context.Repository.GetDashboardShowcaseAsync(authorLimit: 3)).TopAuthors;
        Assert.Equal(expected.Take(3), smaller.Select(author => author.Author));
        var minimum = (await context.Repository.GetDashboardShowcaseAsync(authorLimit: 0)).TopAuthors;
        Assert.Equal(expected[0], Assert.Single(minimum).Author);
        var maximum = (await context.Repository.GetDashboardShowcaseAsync(authorLimit: 99)).TopAuthors;
        Assert.Equal(expected.Take(20), maximum.Select(author => author.Author));
    }

    [Fact]
    public async Task AuthorLimit_OnlyIncludesNamedAuthorsOfCompletedFullyRatedBooks()
    {
        await using var context = await TempDatabase.CreateAsync();
        foreach (var (author, kind, completed, fullyRated) in new[]
        {
            (" Eligible ", "book", true, true),
            ("Viewing", "screen", true, true),
            ("Reading", "book", false, true),
            ("Partial", "book", true, false),
            (" ", "book", true, true)
        })
        {
            var work = new MediaWork { Title = $"Work {author}", Author = author, Kind = kind };
            await context.Repository.AddWorkAsync(work);
            await context.Repository.AddExperienceAsync(new MediaExperience
            {
                WorkId = work.Id, StartedOn = new DateOnly(2026, 10, 1),
                CompletedOn = completed ? new DateOnly(2026, 10, 2) : null,
                Allure = 3, Immersion = 5, Rationality = 5, Illumination = fullyRated ? 5 : null
            });
        }

        var rankedAuthor = Assert.Single((await context.Repository.GetDashboardShowcaseAsync()).TopAuthors);
        Assert.Equal("Eligible", rankedAuthor.Author);
        Assert.Equal(1, rankedAuthor.WorkCount);
        Assert.Equal(1, rankedAuthor.RatingCount);
        Assert.Equal(5.0, rankedAuthor.Score);
    }
}
