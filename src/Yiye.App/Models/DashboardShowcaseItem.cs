namespace Yiye.Models;

public sealed class DashboardShowcaseItem
{
    public string WorkId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Kind { get; init; } = "book";
    public string? Author { get; init; }
    public string? PrimaryCoverPath { get; init; }
    public int CompletionCount { get; init; }
    public int RatingCount { get; init; }
    public double? AggregateRank { get; init; }
    public DateOnly FirstCompletedOn { get; init; }
    public DateOnly LatestCompletedOn { get; init; }

}
