using Yiye.Models;

namespace Yiye.Presentation;

public sealed class AuthorCard(DashboardAuthorRank value, double fontSize = 24)
{
    public DashboardAuthorRank Value { get; } = value;
    public int Position => Value.Position;
    public string Author => Value.Author;
    public double Score => Value.Score;
    public double FontSize { get; } = fontSize;
    public double CompactFontSize => FontSize * 0.9;
    public bool IsVertical => Position > 1 && Position % 3 == 0 && Author.Length is >= 2 and <= 4
        && Author.All(character => character is >= '\u3400' and <= '\u9FFF');
    public string CloudLabel => IsVertical ? string.Join("\n", Author.ToCharArray()) : Author;
    public double VerticalLineHeight => FontSize * 1.15;
    public double CompactVerticalLineHeight => CompactFontSize * 1.15;
    public string WorkCountLabel => $"已评 {Value.WorkCount} 本";

    public string ScoreLabel => Score.ToString("0.0");
    public string Summary => $"{Author}\n{ScoreLabel} / 5.0 · {WorkCountLabel}";

    public static IReadOnlyList<AuthorCard> CreateCloud(IEnumerable<DashboardAuthorRank> authors, int limit = 8)
    {
        var selected = authors.Take(Math.Clamp(limit, 1, 20)).ToArray();
        if (selected.Length == 0) return [];
        var minimum = selected.Min(author => author.Score);
        var maximum = selected.Max(author => author.Score);
        var range = maximum - minimum;
        return selected.Select(author => new AuthorCard(author,
            range < 0.0001 ? 24 : 18 + 12 * (author.Score - minimum) / range)).ToArray();
    }
}
