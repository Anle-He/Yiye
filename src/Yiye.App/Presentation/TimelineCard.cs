using Yiye.Models;

namespace Yiye.Presentation;

public sealed class TimelineCard(DashboardTimelineItem value)
{
    public DashboardTimelineItem Value { get; } = value;
    public string Id => Value.Id;
    public string WorkId => Value.WorkId;
    public string Title => Value.Title;
    public string Kind => Value.Kind;
    public DateOnly LoggedOn => Value.LoggedOn;
    public string? Notes => Value.Notes;
    public string? PrimaryCoverPath => Value.PrimaryCoverPath;

    public bool HasPrimaryCover => !string.IsNullOrWhiteSpace(PrimaryCoverPath);
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
    public string KindGlyph => Kind == "book" ? "书" : "影";
    public string ActionLabel => Kind == "book" ? "完成一次阅读" : "完成一次观看";
    public string NotesExcerpt
    {
        get
        {
            var text = Notes?.Trim() ?? string.Empty;
            return text.Length <= 100 ? text : text[..100] + "…";
        }
    }
}
