using Yiye.Models;

namespace Yiye.Presentation;

public sealed class ExperienceCard(MediaExperience value)
{
    public MediaExperience Value { get; } = value;
    public string Id => Value.Id;
    public string WorkId => Value.WorkId;
    public DateOnly? StartedOn => Value.StartedOn;
    public DateOnly? CompletedOn => Value.CompletedOn;
    public int? Allure => Value.Allure;
    public int? Immersion => Value.Immersion;
    public int? Rationality => Value.Rationality;
    public int? Illumination => Value.Illumination;
    public string? Notes => Value.Notes;
    public DateTimeOffset CreatedAt => Value.CreatedAt;
    public DateTimeOffset UpdatedAt => Value.UpdatedAt;
    public int ProgressEntryCount => Value.ProgressEntryCount;
    public bool HasCompleteRating => Value.HasCompleteRating;
    public double? Rank => Value.Rank;

    public string RankLabel => Rank is null ? "评分未完成" : $"{Rank:0.0} / {RatingScale.RankMaximum:0.0}";
    public string DateRangeLabel => (StartedOn, CompletedOn) switch
    {
        ({ } started, { } completed) when started == completed => started.ToString("yyyy-MM-dd"),
        ({ } started, { } completed) => $"{started:yyyy-MM-dd} — {completed:yyyy-MM-dd}",
        ({ } started, null) => $"始于 {started:yyyy-MM-dd}",
        (null, { } completed) => $"结束于 {completed:yyyy-MM-dd}",
        _ => $"记录于 {CreatedAt.LocalDateTime:yyyy-MM-dd}"
    };
    public string ScoresLabel => HasCompleteRating
        ? $"{Allure} / {Immersion} / {Rationality} / {Illumination}"
        : "四维评分未完成";
    public string NotesLabel => string.IsNullOrWhiteSpace(Notes) ? "没有留下想法" : Notes;
}
