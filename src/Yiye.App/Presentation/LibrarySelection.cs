using Yiye.Models;
namespace Yiye.Presentation;

public sealed class LibrarySelection
{
    public string Kind { get; private set; } = "all";
    public string Query { get; private set; } = string.Empty;
    public string? ExpandedMonthKey { get; private set; }
    public void SetFilter(string kind, string query)
    {
        query = query.Trim();
        if (Kind != kind || Query != query) ExpandedMonthKey = null;
        Kind = kind;
        Query = query;
    }
    public void Expand(string? key) => ExpandedMonthKey = key;
    public IReadOnlyList<WorkCard> Filter(IEnumerable<MediaWork> works) => works.Where(work =>
        (Kind == "all" || work.Kind == Kind) && (Query.Length == 0 ||
        work.Title.Contains(Query, StringComparison.CurrentCultureIgnoreCase) ||
        (work.Subtitle?.Contains(Query, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
        (work.Author?.Contains(Query, StringComparison.CurrentCultureIgnoreCase) ?? false)))
        .Select(work => new WorkCard(work)).ToArray();
}
