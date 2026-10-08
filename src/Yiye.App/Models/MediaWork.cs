namespace Yiye.Models;

public sealed class MediaWork
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Author { get; init; }
    public required string Kind { get; init; }
    public string? Status { get; init; }
    public int? TotalEpisodes { get; init; }
    public string? PrimaryCoverPath { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
    public int ExperienceCount { get; init; }
    public int RatedExperienceCount { get; init; }
    public bool HasActiveExperience { get; init; }
    public double? AggregateRank { get; init; }
    public DateOnly? LatestActivityOn { get; init; }

}
