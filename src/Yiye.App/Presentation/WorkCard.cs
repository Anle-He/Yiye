using Yiye.Models;

namespace Yiye.Presentation;

public sealed class WorkCard(MediaWork value)
{
    public MediaWork Value { get; } = value;
    public string Id => Value.Id;
    public string Title => Value.Title;
    public string? Subtitle => Value.Subtitle;
    public string? Author => Value.Author;
    public string Kind => Value.Kind;
    public string? Status => Value.Status;
    public int? TotalEpisodes => Value.TotalEpisodes;
    public string? PrimaryCoverPath => Value.PrimaryCoverPath;
    public DateTimeOffset CreatedAt => Value.CreatedAt;
    public DateTimeOffset UpdatedAt => Value.UpdatedAt;
    public int ExperienceCount => Value.ExperienceCount;
    public int RatedExperienceCount => Value.RatedExperienceCount;
    public bool HasActiveExperience => Value.HasActiveExperience;
    public double? AggregateRank => Value.AggregateRank;
    public DateOnly? LatestActivityOn => Value.LatestActivityOn;

    public string KindLabel => Kind == "book" ? "书籍" : "影视";
    public string KindGlyph => Kind == "book" ? "书" : "影";
    public string ExperienceActionLabel => Kind == "book" ? "阅读" : "观看";
    public string ExperienceSummaryLabel => ExperienceCount == 0
        ? "尚无完成记录"
        : $"已记录 {ExperienceCount} 次";
    public string AggregateRankLabel => AggregateRank is null
        ? "暂无评分"
        : $"{AggregateRank:0.0} / {RatingScale.RankMaximum:0.0}";
    public bool HasAggregateRank => AggregateRank is not null;
    public string AggregateRankValueLabel => AggregateRank?.ToString("0.0") ?? string.Empty;
    public string AggregateRankMaximumLabel => $"/ {RatingScale.RankMaximum:0.0}";
    public string AggregateScoreTier => ScorePresentation.Tier(AggregateRank);
    public string AggregateScoreTierLabel => AggregateScoreTier switch
    {
        "gold" => "金星",
        "silver" => "银星",
        _ => "铜星"
    };
    public string AggregateRatingMarkLabel => $"{AggregateScoreTierLabel} · {AggregateRankLabel}";
    public string ExperienceCountColorTier => ExperienceCount switch
    {
        >= 2 => "gold",
        1 => "green",
        _ => "muted"
    };
    public string ExperienceCountLabel => $"{ExperienceActionLabel} {ExperienceCount} 次";
    public string RatingCountLabel => RatedExperienceCount == 0 ? "尚无完整评分" : $"来自 {RatedExperienceCount} 次评分";
    public string LatestActivityLabel => LatestActivityOn is null ? "尚未记录" : $"最近 {LatestActivityOn:yyyy-MM-dd}";
    public bool HasPrimaryCover => !string.IsNullOrWhiteSpace(PrimaryCoverPath);
}
