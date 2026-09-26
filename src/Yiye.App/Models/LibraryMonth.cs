namespace Yiye.Models;

public sealed class LibraryMonth
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<MediaWork> Works { get; init; }
    public string CountLabel => $"{Works.Count} 部作品";
    public string OpenLabel => $"展开 {Label}，{CountLabel}";
    public IReadOnlyList<LibraryMonthPreview> PreviewCards => Works.Take(3)
        .Select((work, index) => new LibraryMonthPreview(work,
            index switch { 1 => 80, 2 => 20, _ => 50 },
            index switch { 1 => 16, 2 => 10, _ => 28 },
            index switch { 1 => 10, 2 => -10, _ => -2 }, 3 - index))
        .Reverse().ToArray();

    public static IReadOnlyList<LibraryMonth> Group(IEnumerable<MediaWork> works) => works
        .GroupBy(work => work.LatestActivityOn is { } date ? new DateOnly(date.Year, date.Month, 1) : (DateOnly?)null)
        .OrderByDescending(group => group.Key)
        .Select(group => new LibraryMonth
        {
            Key = group.Key?.ToString("yyyy-MM") ?? "unrecorded",
            Label = group.Key?.ToString("yyyy 年 M 月") ?? "尚无记录",
            Works = group.OrderByDescending(work => work.LatestActivityOn)
                .ThenBy(work => work.Title, StringComparer.CurrentCulture).ToArray()
        }).ToArray();
}

public sealed record LibraryMonthPreview(MediaWork Work, double Left, double Top, double Angle, int Layer);
