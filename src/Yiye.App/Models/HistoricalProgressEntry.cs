namespace Yiye.Models;

public sealed record HistoricalProgressEntry(DateOnly LoggedOn, string Metric, int Amount, string? Notes)
{
    public string DisplayText => $"{LoggedOn:yyyy-MM-dd} · {Amount} {(Metric == "episodes" ? "集" : "分钟")}" +
        (string.IsNullOrWhiteSpace(Notes) ? string.Empty : $"\n{Notes}");
}
