using Yiye.Models;
namespace Yiye.Presentation;

public sealed class ProgressCard(HistoricalProgressEntry value)
{
    public string DisplayText => $"{value.LoggedOn:yyyy-MM-dd} · {value.Amount} {(value.Metric == "episodes" ? "集" : "分钟")}" +
        (string.IsNullOrWhiteSpace(value.Notes) ? string.Empty : $"\n{value.Notes}");
}
