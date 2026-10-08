using Yiye.Models;
namespace Yiye.Data;

public sealed class LibraryRepository
{
    public WorkRepository Works { get; }
    public ExperienceRepository Experiences { get; }
    public CoverRepository Covers { get; }
    public DashboardQueries Dashboard { get; }
    public LibraryRepository(Database database)
    {
        Covers = new(database);
        Works = new(database, Covers);
        Experiences = new(database);
        Dashboard = new(database);
    }
    public Task<IReadOnlyList<MediaWork>> GetWorksAsync() => Works.GetWorksAsync();
    public Task<MediaWork?> GetWorkAsync(string id) => Works.GetWorkAsync(id);
    public Task AddWorkAsync(MediaWork work) => Works.AddWorkAsync(work);
    public Task UpdateWorkMetadataAsync(MediaWork work) => Works.UpdateWorkMetadataAsync(work);
    public Task<IReadOnlyList<MediaExperience>> GetExperiencesAsync(string workId) => Experiences.GetExperiencesAsync(workId);
    public Task<IReadOnlyList<HistoricalProgressEntry>> GetHistoricalProgressAsync(string experienceId) => Experiences.GetHistoricalProgressAsync(experienceId);
    public Task AddExperienceAsync(MediaExperience experience) => Experiences.AddExperienceAsync(experience);
    public Task UpdateExperienceAsync(MediaExperience experience) => Experiences.UpdateExperienceAsync(experience);
    public Task DeleteExperienceAsync(string experienceId, string workId) => Experiences.DeleteExperienceAsync(experienceId, workId);
    public Task<IReadOnlyList<DashboardTimelineItem>> GetRecentTimelineAsync(int limit = 5) => Dashboard.GetRecentTimelineAsync(limit);
    public Task<DashboardShowcase> GetDashboardShowcaseAsync() => Dashboard.GetDashboardShowcaseAsync();
    public Task<IReadOnlyList<WorkCover>> GetCoversAsync(string workId) => Covers.GetCoversAsync(workId);
    public Task AddCoversAsync(string workId, IReadOnlyList<string> sourcePaths) => Covers.AddCoversAsync(workId, sourcePaths);
    public Task SetPrimaryCoverAsync(string workId, string coverId) => Covers.SetPrimaryCoverAsync(workId, coverId);
    public Task MoveCoverAsync(string workId, string coverId, int offset) => Covers.MoveCoverAsync(workId, coverId, offset);
    public Task DeleteCoverAsync(string workId, string coverId) => Covers.DeleteCoverAsync(workId, coverId);
    public Task DeleteWorkAsync(string workId) => Works.DeleteWorkAsync(workId);
}
