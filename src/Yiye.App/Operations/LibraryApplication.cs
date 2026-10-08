using Yiye.Data;
using Yiye.Models;

namespace Yiye.Operations;

public enum WorkChangeKind { Metadata, Experiences, Covers, Removed }
public sealed record WorkChange(string WorkId, WorkChangeKind Kind, MediaWork? Work);
public sealed record WorkDetail(MediaWork Work, IReadOnlyList<WorkCover> Covers, IReadOnlyList<MediaExperience> Experiences);
public sealed record DashboardSnapshot(IReadOnlyList<DashboardTimelineItem> Timeline, DashboardShowcase Showcase);

public sealed class LibraryApplication
{
    private readonly Database _database;
    private readonly SemaphoreSlim _writes = new(1, 1);
    public LibraryApplication(Database database)
    {
        _database = database;
        Covers = new(database);
        Works = new(database, Covers);
        Experiences = new(database);
        Dashboard = new(database);
    }
    public WorkRepository Works { get; }
    public ExperienceRepository Experiences { get; }
    public CoverRepository Covers { get; }
    public DashboardQueries Dashboard { get; }
    public Task InitializeAsync() => _database.InitializeAsync();
    public async Task<WorkDetail?> LoadWorkAsync(string workId)
    {
        var work = await Works.GetWorkAsync(workId);
        if (work is null) return null;
        var covers = await Covers.GetCoversAsync(workId);
        var experiences = await Experiences.GetExperiencesAsync(workId);
        return new(work, covers, experiences);
    }
    public async Task<DashboardSnapshot> LoadDashboardAsync()
    {
        var timeline = await Dashboard.GetRecentTimelineAsync();
        return new(timeline, await Dashboard.GetDashboardShowcaseAsync());
    }
    public Task<WorkChange> AddWorkAsync(MediaWork work) => ChangeAsync(work.Id, WorkChangeKind.Metadata, () => Works.AddWorkAsync(work));
    public Task<WorkChange> EditWorkAsync(MediaWork work) => ChangeAsync(work.Id, WorkChangeKind.Metadata, () => Works.UpdateWorkMetadataAsync(work));
    public Task<WorkChange> SaveCompletionAsync(MediaExperience experience, bool editing)
    {
        if (experience.CompletedOn is null) throw new InvalidOperationException("请选择完成日期。");
        return ChangeAsync(experience.WorkId, WorkChangeKind.Experiences,
            () => editing ? Experiences.UpdateExperienceAsync(experience) : Experiences.AddExperienceAsync(experience));
    }
    public Task<WorkChange> DeleteExperienceAsync(MediaExperience experience) => ChangeAsync(experience.WorkId,
        WorkChangeKind.Experiences, () => Experiences.DeleteExperienceAsync(experience.Id, experience.WorkId));
    public Task<WorkChange> DeleteWorkAsync(string workId) => ChangeAsync(workId, WorkChangeKind.Removed, () => Works.DeleteWorkAsync(workId));
    public async Task<WorkChange> CoversChangedAsync(string workId) => new(workId, WorkChangeKind.Covers, await Works.GetWorkAsync(workId));
    private async Task<WorkChange> ChangeAsync(string workId, WorkChangeKind kind, Func<Task> action)
    {
        await _writes.WaitAsync();
        try
        {
            await action();
            return new(workId, kind, kind == WorkChangeKind.Removed ? null : await Works.GetWorkAsync(workId));
        }
        finally { _writes.Release(); }
    }
}
