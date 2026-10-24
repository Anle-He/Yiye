using Yiye.Models;

namespace Yiye.Presentation;

public sealed class AuthorCard(DashboardAuthorRank value)
{
    public DashboardAuthorRank Value { get; } = value;
    public int Position => Value.Position;
    public string Author => Value.Author;
    public double Score => Value.Score;

    public string PositionLabel => Position.ToString("00");
    public string ScoreLabel => Score.ToString("0.0");
}
