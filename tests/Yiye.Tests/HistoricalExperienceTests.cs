using Yiye.Presentation;
using Yiye.Operations;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using Yiye.Models;

namespace Yiye.Tests;

public sealed class HistoricalExperienceTests
{
    [Fact]
    public async Task HistoricalRecordsRemainAccessibleAndCompleteInPlace()
    {
        await using var context = await TempDatabase.CreateAsync();
        var work = new MediaWork { Title = "history", Kind = "book" };
        await context.Repository.AddWorkAsync(work);
        var active = new MediaExperience { WorkId = work.Id, StartedOn = new DateOnly(2026, 1, 1), Notes = "original note" };
        var undated = new MediaExperience { WorkId = work.Id, Notes = "undated note" };
        await context.Repository.AddExperienceAsync(active);
        await context.Repository.AddExperienceAsync(undated);
        await context.SeedHistoricalProgressAsync(active.Id, new DateOnly(2026, 1, 2));
        var progress = await context.Repository.GetHistoricalProgressAsync(active.Id);
        Assert.Equal("2026-01-02 · 1 分钟", new ProgressCard(Assert.Single(progress)).DisplayText);
        Assert.Empty(await context.Repository.GetHistoricalProgressAsync(undated.Id));

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            App? app = null;
            MainWindow? window = null;
            AddExperienceWindow? dialog = null;
            try
            {
                app = new App(launchWindow: false);
                app.InitializeComponent();
                var library = new LibraryApplication(context.Database);
                window = new MainWindow(library);
                window.InitializeLibraryAsync().GetAwaiter().GetResult();
                window.OpenWorkAsync(work.Id).GetAwaiter().GetResult();
                Assert.Equal(2, window.DetailView.UnfinishedExperiences.Count);
                Assert.Empty(window.DetailView.CompletedExperiences);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.DetailView.FindName("UnfinishedHistory")).Visibility);
                dialog = new AddExperienceWindow(work.Id, work.Kind, active, progress);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)dialog.FindName("DeleteButton")).Visibility);
                Assert.Contains("2026-01-02", ((TextBlock)dialog.FindName("HistoricalProgressText")).Text);
                Assert.Equal(active.Notes, ((TextBox)dialog.FindName("NotesBox")).Text);
                ((DatePicker)dialog.FindName("CompletedOnPicker")).SelectedDate = new DateTime(2026, 1, 3);
                dialog.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { ((Button)dialog.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
                    catch (Exception exception) { failure = exception; dialog.Close(); }
                }));
                Assert.True(dialog.ShowDialog());
                var completed = Assert.IsType<MediaExperience>(dialog.Experience);
                Assert.Equal(active.Id, completed.Id);
                Assert.Equal(active.StartedOn, completed.StartedOn);
                Assert.Equal(active.Notes, completed.Notes);
                context.Repository.UpdateExperienceAsync(completed).GetAwaiter().GetResult();
                window.OpenWorkAsync(work.Id).GetAwaiter().GetResult();
                Assert.Equal(undated.Id, Assert.Single(window.DetailView.UnfinishedExperiences).Id);
                Assert.Equal(active.Id, Assert.Single(window.DetailView.CompletedExperiences).Experience.Id);
                AssertNewCompletionMovesWorkBetweenMonths(library, window);
            }
            catch (Exception exception) { failure = exception; }
            finally { dialog?.Close(); window?.Close(); app?.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.Equal(2, (await context.Repository.GetExperiencesAsync(work.Id)).Count);
        Assert.Equal(1L, await context.CountHistoricalProgressAsync(active.Id));
        await context.Repository.DeleteExperienceAsync(active.Id, work.Id);
        Assert.Empty(await context.Repository.GetHistoricalProgressAsync(active.Id));
        Assert.Equal(undated.Id, Assert.Single(await context.Repository.GetExperiencesAsync(work.Id)).Id);
    }

    private static void AssertNewCompletionMovesWorkBetweenMonths(LibraryApplication library, MainWindow window)
    {
        var work = new MediaWork { Title = "month transition", Kind = "book" };
        window.ApplyChangeAsync(library.AddWorkAsync(work).GetAwaiter().GetResult()).GetAwaiter().GetResult();
        window.ApplyChangeAsync(library.SaveCompletionAsync(new MediaExperience
        {
            WorkId = work.Id,
            CompletedOn = new DateOnly(2026, 9, 30)
        }, editing: false).GetAwaiter().GetResult()).GetAwaiter().GetResult();
        window.ShowLibrary();
        var view = window.LibraryView;
        view.OpenMonth("2026-09");
        Assert.Equal(work.Id, Assert.Single(view.ExpandedMonthWorks).Id);

        window.ApplyChangeAsync(library.SaveCompletionAsync(new MediaExperience
        {
            WorkId = work.Id,
            CompletedOn = new DateOnly(2026, 10, 1)
        }, editing: false).GetAwaiter().GetResult()).GetAwaiter().GetResult();

        Assert.DoesNotContain(view.LibraryMonths, month => month.Key == "2026-09");
        var october = Assert.Single(view.LibraryMonths, month => month.Key == "2026-10");
        Assert.Equal(work.Id, Assert.Single(october.Works).Id);
        Assert.Single(view.LibraryMonths.SelectMany(month => month.Works), card => card.Id == work.Id);
        Assert.Empty(view.ExpandedMonthWorks);
        Assert.Null(view.Selection.ExpandedMonthKey);
        Assert.Equal(Visibility.Visible, ((ScrollViewer)view.FindName("MonthOverview")).Visibility);
    }
}
