namespace Yiye.Models;

public sealed class MediaExperience
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string WorkId { get; init; }
    public DateOnly? StartedOn { get; init; }
    public DateOnly? CompletedOn { get; init; }
    public int? Allure { get; init; }
    public int? Immersion { get; init; }
    public int? Rationality { get; init; }
    public int? Illumination { get; init; }
    public string? Notes { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
    public int ProgressEntryCount { get; init; }

    public bool HasCompleteRating => Allure is not null && Immersion is not null && Rationality is not null && Illumination is not null;
    public double? Rank => RatingScale.Calculate(Allure, Immersion, Rationality, Illumination);
}
