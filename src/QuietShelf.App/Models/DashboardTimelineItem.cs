namespace QuietShelf.Models;

public sealed class DashboardTimelineItem
{
    public required string Id { get; init; }
    public required string WorkId { get; init; }
    public required string Title { get; init; }
    public required string Kind { get; init; }
    public DateOnly LoggedOn { get; init; }
    public string? Notes { get; init; }
    public string? PrimaryCoverPath { get; init; }

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
