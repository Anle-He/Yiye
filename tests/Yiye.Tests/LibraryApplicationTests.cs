using Yiye.Models;
using Yiye.Operations;
using Yiye.Presentation;

namespace Yiye.Tests;

public sealed class LibraryApplicationTests
{
    [Fact]
    public async Task CompletionChangesRefreshTheAffectedWorkAndPreserveHistory()
    {
        await using var context = await TempDatabase.CreateAsync();
        var library = new LibraryApplication(context.Database);
        var work = new MediaWork { Title = "record", Kind = "book" };
        var added = await library.AddWorkAsync(work);
        Assert.Equal(work.Id, added.WorkId);
        var active = new MediaExperience { WorkId = work.Id, StartedOn = new DateOnly(2026, 1, 1), Notes = "note" };
        await context.Repository.AddExperienceAsync(active);
        await context.SeedHistoricalProgressAsync(active.Id, new DateOnly(2026, 1, 2));
        var completed = new MediaExperience
        {
            Id = active.Id,
            WorkId = work.Id,
            StartedOn = active.StartedOn,
            CompletedOn = new DateOnly(2026, 1, 3),
            Notes = active.Notes,
            Allure = 3,
            Immersion = 5,
            Rationality = 5,
            Illumination = 5,
            CreatedAt = active.CreatedAt
        };
        var change = await library.SaveCompletionAsync(completed, editing: true);
        Assert.Equal(WorkChangeKind.Experiences, change.Kind);
        Assert.Equal(1, change.Work!.ExperienceCount);
        Assert.Equal(3.9, change.Work.AggregateRank);
        Assert.Equal("completed", change.Work.Status);
        var detail = Assert.IsType<WorkDetail>(await library.LoadWorkAsync(work.Id));
        Assert.Equal(active.Id, Assert.Single(detail.Experiences).Id);
        Assert.Equal(1, detail.Experiences[0].ProgressEntryCount);
        Assert.Equal(work.Id, Assert.Single((await library.LoadDashboardAsync()).Timeline).WorkId);
        var deletion = await library.DeleteExperienceAsync(completed);
        Assert.Equal(0, deletion.Work!.ExperienceCount);
        Assert.Null(deletion.Work.AggregateRank);
        Assert.Empty(await library.Experiences.GetHistoricalProgressAsync(active.Id));
    }

    [Fact]
    public async Task CompletionWritesAreSerializedWithoutLosingRecords()
    {
        await using var context = await TempDatabase.CreateAsync();
        var library = new LibraryApplication(context.Database);
        var work = new MediaWork { Title = "concurrent", Kind = "screen" };
        await library.AddWorkAsync(work);
        await Task.WhenAll(Enumerable.Range(1, 8).Select(day => Task.Run(() => library.SaveCompletionAsync(
            new MediaExperience { WorkId = work.Id, CompletedOn = new DateOnly(2026, 1, day) }, editing: false))));
        Assert.Equal(8, (await library.Works.GetWorkAsync(work.Id))!.ExperienceCount);
        var removed = await library.DeleteWorkAsync(work.Id);
        Assert.Equal(WorkChangeKind.Removed, removed.Kind);
        Assert.Null(removed.Work);
        Assert.Null(await library.LoadWorkAsync(work.Id));
    }

    [Fact]
    public async Task InvalidCompletionNeverCreatesARecord()
    {
        await using var context = await TempDatabase.CreateAsync();
        var library = new LibraryApplication(context.Database);
        var work = new MediaWork { Title = "validation", Kind = "book" };
        await library.AddWorkAsync(work);
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.SaveCompletionAsync(new MediaExperience { WorkId = work.Id }, editing: false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.SaveCompletionAsync(new MediaExperience
        { WorkId = work.Id, StartedOn = new DateOnly(2026, 1, 3), CompletedOn = new DateOnly(2026, 1, 2) }, editing: false));
        Assert.Empty(await library.Experiences.GetExperiencesAsync(work.Id));
    }

    [Fact]
    public void LibraryFilterPreservesMonthUntilTheFilterChanges()
    {
        var selection = new LibrarySelection();
        selection.SetFilter("book", "Writer");
        selection.Expand("2026-01");
        selection.SetFilter("book", " Writer ");
        Assert.Equal("2026-01", selection.ExpandedMonthKey);
        var book = new MediaWork { Title = "title", Author = "Writer", Kind = "book" };
        Assert.Same(book, Assert.Single(selection.Filter([book, new MediaWork { Title = "Writer", Kind = "screen" }])).Value);
        selection.SetFilter("screen", "Writer");
        Assert.Null(selection.ExpandedMonthKey);
        Assert.Empty(selection.Filter([book]));
    }
}
