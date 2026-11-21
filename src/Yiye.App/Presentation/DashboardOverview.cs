using Yiye.Models;

namespace Yiye.Presentation;

public sealed record DashboardOverview(int BookCount, int ScreenCount)
{
    public int CompletedWorkCount => BookCount + ScreenCount;
    public double BookShare => CompletedWorkCount == 0 ? 0 : (double)BookCount / CompletedWorkCount;
    public double ScreenShare => CompletedWorkCount == 0 ? 0 : (double)ScreenCount / CompletedWorkCount;

    public static DashboardOverview FromCompletedWorks(IEnumerable<DashboardShowcaseItem> works)
    {
        var completed = works.Where(work => work.CompletionCount > 0).DistinctBy(work => work.WorkId).ToArray();
        return new(completed.Count(work => work.Kind == "book"), completed.Count(work => work.Kind == "screen"));
    }
}
