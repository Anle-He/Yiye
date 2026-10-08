namespace Yiye.Models;

public sealed class DashboardAuthorRank
{
    public const double ScoreMaximum = 5.0;

    public int Position { get; init; }
    public string Author { get; init; } = string.Empty;
    public int WorkCount { get; init; }
    public int RatingCount { get; init; }
    public double WeightedRank { get; init; }

}
