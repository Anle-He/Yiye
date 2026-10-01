namespace Yiye.Models;

public sealed class DashboardAuthorRank
{
    public const double ScoreMaximum = 5.0;

    public int Position { get; init; }
    public string Author { get; init; } = string.Empty;
    public int WorkCount { get; init; }
    public int RatingCount { get; init; }
    public double WeightedRank { get; init; }

    public string PositionLabel => Position.ToString("00");
    public string WeightedRankLabel => WeightedRank.ToString("0.0");
    public string EvidenceLabel => $"{WorkCount} 本书 · {RatingCount} 次完整评分 · 满分 {ScoreMaximum:0.0}";
}
