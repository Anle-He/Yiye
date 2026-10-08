using Yiye.Models;

namespace Yiye.Presentation;

public sealed class ShowcaseCard(DashboardShowcaseItem value)
{
    public DashboardShowcaseItem Value { get; } = value;
    public string WorkId => Value.WorkId;
    public string Title => Value.Title;
    public string Kind => Value.Kind;
    public string? Author => Value.Author;
    public string? PrimaryCoverPath => Value.PrimaryCoverPath;
    public int CompletionCount => Value.CompletionCount;
    public int RatingCount => Value.RatingCount;
    public double? AggregateRank => Value.AggregateRank;
    public DateOnly FirstCompletedOn => Value.FirstCompletedOn;
    public DateOnly LatestCompletedOn => Value.LatestCompletedOn;

    public bool HasPrimaryCover => !string.IsNullOrWhiteSpace(PrimaryCoverPath);
    public string KindGlyph => Kind == "book" ? "书" : "影";
    public string RankLabel => AggregateRank is null ? "未评分" : AggregateRank.Value.ToString("0.0");
    public string RankMaximumLabel => AggregateRank is null ? string.Empty : $"/ {RatingScale.RankMaximum:0.0}";
    public string CompletionLabel => CompletionCount == 1 ? "完成 1 次" : $"完成 {CompletionCount} 次";
    public string LatestDateLabel => LatestCompletedOn.ToString("yyyy.MM.dd");
}
