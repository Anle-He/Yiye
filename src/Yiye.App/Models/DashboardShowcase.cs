namespace Yiye.Models;

public sealed class DashboardShowcase
{
    public IReadOnlyList<DashboardShowcaseItem> CompletedWorks { get; init; } = [];
    public IReadOnlyList<DashboardAuthorRank> TopAuthors { get; init; } = [];
}
