using Yiye.Models;

namespace Yiye.Presentation;

public sealed class AuthorCard(DashboardAuthorRank value)
{
    public DashboardAuthorRank Value { get; } = value;
    public int Position => Value.Position;
    public string Author => Value.Author;
    public int WorkCount => Value.WorkCount;
    public int RatingCount => Value.RatingCount;
    public double WeightedRank => Value.WeightedRank;
    public const double ScoreMaximum = DashboardAuthorRank.ScoreMaximum;

    public string PositionLabel => Position.ToString("00");
    public string WeightedRankLabel => WeightedRank.ToString("0.0");
    public string EvidenceLabel => $"{WorkCount} 本书 · {RatingCount} 次完整评分 · 满分 {ScoreMaximum:0.0}";
}
