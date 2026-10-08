namespace Yiye.Models;

public sealed class DashboardTimelineItem
{
    public required string Id { get; init; }
    public required string WorkId { get; init; }
    public required string Title { get; init; }
    public required string Kind { get; init; }
    public DateOnly LoggedOn { get; init; }
    public string? Notes { get; init; }
    public string? PrimaryCoverPath { get; init; }

}
