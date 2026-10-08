namespace Yiye.Models;

public sealed record HistoricalProgressEntry(DateOnly LoggedOn, string Metric, int Amount, string? Notes);
