using Yiye.Operations;
using Yiye.Presentation;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Yiye.Models;

namespace Yiye.Tests;

public sealed class UiSmokeTests
{
    [Fact]
    [Trait("Category", "Manual")]
    public async Task CoreWindowInteractionsAndAdaptiveLayoutsPreserveLibraryBehavior()
    {
        await using var context = await TempDatabase.CreateAsync();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new Yiye.App(launchWindow: false);
                application.InitializeComponent();
                var mainWindow = new MainWindow(new LibraryApplication(context.Database));
                var layoutCover = Path.Combine(context.Root, "layout-cover.png");
                var layoutEncoder = new PngBitmapEncoder();
                layoutEncoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 3, 96, 96,
                    PixelFormats.Bgr24, null, Enumerable.Repeat((byte)120, 18).ToArray(), 6)));
                using (var output = File.Create(layoutCover)) layoutEncoder.Save(output);
                AssertExperienceRatingTemplate(mainWindow);
                AssertTimelineTemplate(mainWindow);
                AssertDashboardLayout(mainWindow, layoutCover);
                AssertDetailLayout(context, layoutCover);
                AssertLibraryLayout(context, layoutCover);
                AssertMonthFiltering();
                var addWork = new AddWorkWindow();
                var addExperience = new AddExperienceWindow("ui-test", "book");
                var allureBox = Assert.IsType<ComboBox>(addExperience.FindName("AllureBox"));
                Assert.Equal(4, allureBox.Items.Count);
                var completedOn = Assert.IsType<DatePicker>(addExperience.FindName("CompletedOnPicker"));
                Assert.NotNull(completedOn.SelectedDate);
                var dateError = Assert.IsType<TextBlock>(addExperience.FindName("DateError"));
                var saveButton = Assert.IsAssignableFrom<Button>(addExperience.FindName("SaveButton"));
                var deleteButton = Assert.IsAssignableFrom<Button>(addExperience.FindName("DeleteButton"));
                Assert.Equal(Visibility.Collapsed, deleteButton.Visibility);
                completedOn.SelectedDate = null;
                saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(Visibility.Visible, dateError.Visibility);
                Assert.Null(addExperience.Experience);

                var editExperience = new AddExperienceWindow("ui-test", "book", new MediaExperience
                {
                    WorkId = "ui-test",
                    CompletedOn = new DateOnly(2026, 8, 29),
                    Illumination = 4
                });
                Assert.Equal(Visibility.Visible, Assert.IsAssignableFrom<Button>(editExperience.FindName("DeleteButton")).Visibility);
                AssertRatingSelectionIsSaved(editExperience, editing: true);
                editExperience.Close();

                var covers = new ManageCoversWindow(context.Repository.Covers, new Yiye.Models.MediaWork
                {
                    Id = "ui-cover-work",
                    Title = "ui-cover-test",
                    Kind = "book"
                });
                covers.Show();
                covers.UpdateLayout();
                Assert.True(covers.ActualWidth >= 720);
                Assert.True(covers.ActualHeight >= 540);
                SaveSnapshot(covers, "cover-gallery.png");
                covers.Close();

                SnapshotWindow(addWork, "add-work.png");
                SnapshotWindow(addExperience, "add-experience.png");
                completedOn.SelectedDate = new DateTime(2026, 10, 1);
                AssertRatingSelectionIsSaved(addExperience, editing: false);

                AssertExplicitWorkSelection(context);
                AssertCoverLoadFailure(context, application);
                AssertCoverSaveFinishesBeforeClosing(context, application);
                AssertNavigationAndAdaptiveLayout(context);

                addExperience.Close();
                addWork.Close();
                mainWindow.Close();
                application.Shutdown();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void AssertRatingSelectionIsSaved(AddExperienceWindow window, bool editing)
    {
        Exception? failure = null;
        window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            try
            {
                foreach (var (name, rating) in new[] { ("AllureBox", 3), ("ImmersionBox", 1), ("RationalityBox", 4), ("IlluminationBox", 2) })
                {
                    var box = Assert.IsType<ComboBox>(Find(window, name));
                    box.BringIntoView();
                    window.UpdateLayout();
                    DrainDispatcher(window.Dispatcher);
                    Assert.True(box.IsLoaded);
                    Assert.True(box.Focus(), $"{name} must accept keyboard focus before input.");
                    DrainDispatcher(window.Dispatcher);
                    Assert.True(box.IsKeyboardFocusWithin);
                    var expected = editing && name == "IlluminationBox" ? 4 : 0;
                    Assert.Equal(expected, box.SelectedIndex);
                    PressKey(box, Key.F4);
                    Assert.True(box.IsDropDownOpen, $"F4 should open {name} without changing its rating.");
                    Assert.Equal(expected, box.SelectedIndex);
                    PressKey(box, Key.Home);
                    for (var step = 0; step < rating; step++) PressKey(box, Key.Down);
                    PressKey(box, Key.Enter);
                    Assert.False(box.IsDropDownOpen);
                    Assert.Equal(rating, box.SelectedIndex);
                    PressKey(box, Key.F4);
                    Assert.True(box.IsDropDownOpen);
                    Assert.Equal(rating, box.SelectedIndex);
                    PressKey(box, Key.Escape);
                    Assert.False(box.IsDropDownOpen);
                }
                Assert.Equal("2.3 / 3.9", ((TextBlock)Find(window, "RankPreview")).Text);
                ((Button)Find(window, "SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception exception) { failure = exception; window.Close(); }
        }));
        var saved = window.ShowDialog();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.True(saved);
        var experience = Assert.IsType<MediaExperience>(window.Experience);
        Assert.Equal(3, experience.Allure);
        Assert.Equal(1, experience.Immersion);
        Assert.Equal(4, experience.Rationality);
        Assert.Equal(2, experience.Illumination);
        Assert.Equal(editing ? new DateOnly(2026, 8, 29) : new DateOnly(2026, 10, 1), experience.CompletedOn);
    }

    private static void PressKey(ComboBox box, Key key)
    {
        // Exercise WPF's keyboard handlers without requiring foreground desktop input.
        var input = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(box), Environment.TickCount, key)
        { RoutedEvent = Keyboard.KeyDownEvent };
        box.RaiseEvent(input);
        box.UpdateLayout();
        // ComboBox queues popup focus and selection work; finish it before the next key.
        DrainDispatcher(box.Dispatcher);
    }

    private static void DrainDispatcher(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void AssertExplicitWorkSelection(TempDatabase context)
    {
        var book = new MediaWork { Title = "existing-book", Kind = "book" };
        var screen = new MediaWork { Title = "new-screen", Kind = "screen" };
        context.Repository.AddWorkAsync(book).GetAwaiter().GetResult();
        context.Repository.AddWorkAsync(screen).GetAwaiter().GetResult();
        var library = new LibraryApplication(context.Database);
        var window = new MainWindow(library);
        try
        {
            window.InitializeLibraryAsync().GetAwaiter().GetResult();
            foreach (var (kind, target) in new[] { ("book", screen), ("screen", book), ("book", book) })
            {
                window.LibraryView.SetFilter(kind, "");
                window.OpenWorkAsync(target.Id).GetAwaiter().GetResult();
                Assert.Equal(target.Id, window.DetailView.Work!.Id);
                Assert.Equal(target.Title, ((TextBlock)Find(window, "DetailTitleText")).Text);
                Assert.Equal(kind, window.LibraryView.Selection.Kind);
            }
            var experience = new MediaExperience { WorkId = book.Id, CompletedOn = new DateOnly(2026, 10, 1),
                Allure = 3, Immersion = 5, Rationality = 5, Illumination = 5 };
            window.ApplyChangeAsync(library.SaveCompletionAsync(experience, editing: false).GetAwaiter().GetResult()).GetAwaiter().GetResult();
            Assert.Equal(book.Id, window.DetailView.Work!.Id);
            Assert.Single(window.DetailView.CompletedExperiences);
            Assert.Equal("1", ((TextBlock)Find(window, "DashboardExperienceCountText")).Text);
            Assert.Equal(book.Id, Assert.Single(window.HomeView.DashboardTopBooks).WorkId);
            window.ApplyChangeAsync(library.DeleteWorkAsync(screen.Id).GetAwaiter().GetResult()).GetAwaiter().GetResult();
            Assert.Equal(book.Id, window.DetailView.Work.Id);
            window.ShowLibrary();
            Assert.Equal(LibraryPageKind.Library, window.CurrentPage);
            Assert.Null(window.DetailView.Work);
            window.ShowHome();
            Assert.Equal(LibraryPageKind.Home, window.CurrentPage);
        }
        finally { window.Close(); }
    }

    private static void AssertNavigationAndAdaptiveLayout(TempDatabase context)
    {
        var works = Enumerable.Range(1, 20).Select(index => new MediaWork
            { Title = $"navigation-{index:00}", Kind = "book", Subtitle = index == 1 ? "康熙废储与正史虚构" : null,
              Author = index == 1 ? "孙立天" : null }).ToArray();
        foreach (var work in works)
        {
            context.Repository.AddWorkAsync(work).GetAwaiter().GetResult();
            context.Repository.AddExperienceAsync(new MediaExperience
            {
                WorkId = work.Id, CompletedOn = new DateOnly(2026, 9, 20),
                Allure = 3, Immersion = 4, Rationality = 5, Illumination = 4
            }).GetAwaiter().GetResult();
        }
        // Exercise motion with an actual cover, and navigation without one.
        var imagePath = Path.Combine(context.Root, "cover-save.png");
        context.Repository.Covers.AddCoversAsync(works[0].Id, new[] { imagePath }).GetAwaiter().GetResult();
        var window = new MainWindow(new LibraryApplication(context.Database));
        try
        {
            window.InitializeLibraryAsync().GetAwaiter().GetResult();
            window.Show();
            window.Width = 1280; window.Height = 800;
            window.ShowHome();
            var search = (TextBox)Find(window, "GlobalSearchBox");
            search.Text = "navigation";
            search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(search),
                Environment.TickCount, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
            window.UpdateLayout();
            Assert.Equal(LibraryPageKind.Library, window.CurrentPage);
            Assert.Equal("navigation", window.LibraryView.Selection.Query);
            Assert.Equal("all", window.LibraryView.Selection.Kind);
            Assert.Equal(works.Select(work => work.Id).Order(), window.LibraryView.VisibleWorks.Select(work => work.Id).Order());
            Assert.True(search.IsKeyboardFocused);
            window.LibraryView.SetFilter("book", "navigation");
            window.LibraryView.OpenMonth("2026-09");
            window.UpdateLayout();
            window.OpenWorkAsync(works[0].Id).GetAwaiter().GetResult();
            var transitionLayer = (Canvas)window.FindName("CoverTransitionLayer");
            if (SystemParameters.ClientAreaAnimation) Assert.Single(transitionLayer.Children.Cast<UIElement>());
            SettleAnimations();
            Assert.Empty(transitionLayer.Children.Cast<UIElement>());
            AssertScrollAccess((ScrollViewer)Find(window, "DetailScroll"));
            Assert.NotNull(window.DetailView.CoverImageSource);
            Assert.True(System.Windows.Controls.Primitives.Selector.GetIsSelected((Button)Find(window, "LibraryButton")));
            Assert.Contains("2026", ((Button)Find(window, "BackButton")).Content.ToString());
            window.GoBack();
            Assert.Equal(LibraryPageKind.Library, window.CurrentPage);
            Assert.Equal("2026-09", window.LibraryView.Selection.ExpandedMonthKey);
            Assert.Equal("navigation", window.LibraryView.Selection.Query);
            Assert.Equal("book", window.LibraryView.Selection.Kind);
            var list = (ListBox)Find(window, "WorkList");
            Assert.Null(list.SelectedItem);
            window.UpdateLayout();
            var scroll = Descendants(list).OfType<ScrollViewer>().Single();
            Assert.InRange(scroll.ScrollableHeight, 0, 1);
            Assert.True(window.LibraryView.PageCount > 1);
            window.LibraryView.SetPage(window.LibraryView.PageCount - 1);
            window.UpdateLayout();
            var pageIndex = window.LibraryView.PageIndex;
            var pageWork = window.LibraryView.PageWorks[^1];
            window.OpenWorkAsync(pageWork.Id).GetAwaiter().GetResult();
            window.GoBack(); window.UpdateLayout();
            DrainDispatcher(window.Dispatcher);
            Assert.Equal(pageIndex, window.LibraryView.PageIndex);
            Assert.True(Descendants(list).OfType<ListBoxItem>().Any(item =>
                item.DataContext is WorkCard card && card.Id == pageWork.Id && item.IsKeyboardFocusWithin),
                "Returning from a work should restore focus to that work in its library group.");

            window.OpenWorkAsync(works[0].Id).GetAwaiter().GetResult();
            window.Width = 980; window.Height = 640; window.UpdateLayout();
            AssertDetailActions(window);
            SettleAnimations();
            var compactDetailScroll = (ScrollViewer)Find(window, "DetailScroll");
            Assert.InRange(compactDetailScroll.ScrollableHeight, 0, 1);
            AssertWithin((FrameworkElement)Find(window, "SummarySurface"), compactDetailScroll);
            SaveSnapshot(window, "navigation-detail-compact.png");
            window.Width = 1400; window.Height = 900; window.UpdateLayout();
            AssertDetailActions(window);
            SettleAnimations();
            SaveSnapshot(window, "navigation-detail-wide.png");
            window.ShowHome();
            window.OpenWorkAsync(works[1].Id).GetAwaiter().GetResult();
            Assert.Equal("返回首页", ((Button)Find(window, "BackButton")).Content);
            Assert.True(System.Windows.Controls.Primitives.Selector.GetIsSelected((Button)Find(window, "HomeButton")));
            window.GoBack();
            Assert.Equal(LibraryPageKind.Home, window.CurrentPage);
        }
        finally { window.Close(); }

        var dialog = new AddExperienceWindow(works[0].Id, "book");
        try
        {
            dialog.Show(); dialog.Width = 920; dialog.Height = 650; dialog.UpdateLayout();
            var rating = (FrameworkElement)dialog.FindName("RatingSection");
            var notes = (FrameworkElement)dialog.FindName("NotesSection");
            Assert.Equal(2, Grid.GetColumn(rating));
            var viewport = (ScrollViewer)dialog.FindName("FormScroll");
            var bounds = notes.TransformToAncestor(viewport).TransformBounds(new Rect(notes.RenderSize));
            Assert.True(bounds.Bottom <= viewport.ActualHeight + 1, $"Summary must be visible: bottom={bounds.Bottom}, viewport={viewport.ActualHeight}");
            ((TextBox)dialog.FindName("NotesBox")).Text = "保留这一次的总结";
            ((ComboBox)dialog.FindName("AllureBox")).SelectedIndex = 3;
            SaveSnapshot(dialog, "completion-wide.png");
            dialog.Width = 580; dialog.Height = 600; dialog.UpdateLayout();
            Assert.Equal(0, Grid.GetColumn(rating));
            Assert.Equal(1, Grid.GetRow(rating));
            Assert.Equal("保留这一次的总结", ((TextBox)dialog.FindName("NotesBox")).Text);
            Assert.Equal(3, ((ComboBox)dialog.FindName("AllureBox")).SelectedIndex);
            Assert.True(((Button)dialog.FindName("SaveButton")).IsVisible);
            SaveSnapshot(dialog, "completion-narrow.png");
        }
        finally { dialog.Close(); }
    }

    private static void SettleAnimations()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void AssertCoverSaveFinishesBeforeClosing(TempDatabase context, Application application)
    {
        var work = new MediaWork { Title = "cover-save-close", Kind = "book" };
        context.Repository.AddWorkAsync(work).GetAwaiter().GetResult();
        var imagePath = Path.Combine(context.Root, "cover-save.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 96, 96,
            PixelFormats.Bgr24, null, new byte[] { 0, 0, 0 }, 3)));
        using (var output = File.Create(imagePath)) encoder.Save(output);

        var window = new ManageCoversWindow(context.Repository.Covers, work);
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(application.Dispatcher));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var execute = typeof(ManageCoversWindow).GetMethod("ExecuteAndReloadAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        try
        {
            window.Show();
            Func<Task> save = async () =>
            {
                await release.Task;
                await context.Repository.Covers.AddCoversAsync(work.Id, new[] { imagePath });
            };
            var operation = (Task)execute.Invoke(window, new object[] { save, "save failed" })!;
            Assert.False(window.IsEnabled);
            window.Close();
            Assert.True(window.IsVisible);
            Assert.False(window.HasChanges);

            var secondActionRan = false;
            Func<Task> secondAction = () => { secondActionRan = true; return Task.CompletedTask; };
            ((Task)execute.Invoke(window, new object[] { secondAction, "save failed" })!).GetAwaiter().GetResult();
            Assert.False(secondActionRan);

            release.SetResult();
            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            timeout.Tick += (_, _) => frame.Continue = false;
            timeout.Start();
            _ = operation.ContinueWith(_ => application.Dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
            Dispatcher.PushFrame(frame);
            timeout.Stop();
            Assert.True(operation.IsCompleted);
            operation.GetAwaiter().GetResult();
            Assert.True(window.HasChanges);
            Assert.True(window.IsEnabled);
            Assert.Single(window.Covers);
            Assert.Single(context.Repository.Covers.GetCoversAsync(work.Id).GetAwaiter().GetResult());
            AssertCoverPreviewKeyboardNavigation(window);
            window.Close();
            Assert.False(window.IsVisible);
        }
        finally
        {
            release.TrySetResult();
            window.Close();
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    private static void AssertCoverPreviewKeyboardNavigation(ManageCoversWindow window)
    {
        window.UpdateLayout();
        var previewButton = Assert.Single(Descendants(window).OfType<Button>(),
            button => System.Windows.Automation.AutomationProperties.GetName(button) == "查看封面大图");
        var overlay = Assert.IsType<Grid>(Find(window, "PreviewOverlay"));
        var gallery = Assert.IsType<Grid>(Find(window, "GalleryBody"));
        var closeButton = Assert.IsAssignableFrom<Button>(Find(window, "ClosePreviewButton"));
        var image = Assert.IsType<Image>(Find(window, "PreviewImage"));
        Assert.True(previewButton.Focus());
        Assert.Same(previewButton, Keyboard.FocusedElement);
        SaveSnapshot(window, "cover-gallery-populated.png");

        previewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
        Assert.Equal(Visibility.Visible, overlay.Visibility);
        Assert.False(gallery.IsEnabled);
        Assert.NotNull(image.Source);
        Assert.Same(closeButton, Keyboard.FocusedElement);
        SaveSnapshot(window, "cover-preview.png");

        var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(closeButton), Environment.TickCount, Key.Escape)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        closeButton.RaiseEvent(escape);
        window.UpdateLayout();
        Assert.True(escape.Handled);
        Assert.Equal(Visibility.Collapsed, overlay.Visibility);
        Assert.True(gallery.IsEnabled);
        Assert.Null(image.Source);
        Assert.Same(previewButton, Keyboard.FocusedElement);
    }

    private static void AssertCoverLoadFailure(TempDatabase context, Application application)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(context.Database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE work_covers RENAME TO unavailable_covers;";
        command.ExecuteNonQuery();
        var window = new ManageCoversWindow(context.Repository.Covers, new MediaWork { Title = "load-failure", Kind = "book" });
        var previousContext = SynchronizationContext.Current;
        Exception? unhandled = null;
        DispatcherUnhandledExceptionEventHandler handler = (_, e) => { unhandled = e.Exception; e.Handled = true; };
        application.DispatcherUnhandledException += handler;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(application.Dispatcher));
        try
        {
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            var frame = new DispatcherFrame();
            application.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Assert.Null(unhandled);
            Assert.StartsWith("无法加载封面：", ((TextBlock)Find(window, "CoverCountText")).Text);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)Find(window, "EmptyState")).Visibility);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)Find(window, "CoverScroll")).Visibility);

            command.CommandText = "ALTER TABLE unavailable_covers RENAME TO work_covers;";
            command.ExecuteNonQuery();
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.Equal(Visibility.Visible, ((FrameworkElement)Find(window, "EmptyState")).Visibility);
            Assert.DoesNotContain("无法加载封面", ((TextBlock)Find(window, "CoverCountText")).Text);
        }
        finally
        {
            application.DispatcherUnhandledException -= handler;
            SynchronizationContext.SetSynchronizationContext(previousContext);
            window.Close();
        }
    }

    private static void AssertDashboardLayout(MainWindow window, string coverPath)
    {
        var samples = Enumerable.Range(1, 6).Select(index => new DashboardShowcaseItem
        {
            WorkId = index.ToString(),
            Title = index == 6 ? "一部标题很长的作品，封面与标题仍应可以辨认和打开" : $"收藏作品 {index}",
            Kind = index <= 3 ? "book" : "screen",
            Author = "示例作者",
            PrimaryCoverPath = coverPath,
            AggregateRank = 3.5,
            RatingCount = 2,
            CompletionCount = 2,
            FirstCompletedOn = new DateOnly(2026, 1, index),
            LatestCompletedOn = new DateOnly(2026, 8, index)
        }).ToArray();
        void Populate(Yiye.Views.DashboardView target)
        {
            foreach (var work in samples)
            {
                (work.Kind == "book" ? target.DashboardTopBooks : target.DashboardTopScreens).Add(new ShowcaseCard(work));
                target.DashboardRecentWorks.Add(new ShowcaseCard(work));
                if (target.DashboardTimelineDays.Count < 3) target.DashboardTimelineDays.Add(new DashboardTimelineDay
                {
                    Date = work.LatestCompletedOn,
                    Items = [new TimelineCard(new DashboardTimelineItem
                    {
                        Id = $"event-{work.WorkId}", WorkId = work.WorkId, Title = work.Title, Kind = work.Kind
                    })]
                });
            }
            for (var index = 1; index <= 3; index++)
                target.DashboardTopAuthors.Add(new AuthorCard(new DashboardAuthorRank
                { Position = index, Author = $"作者 {index}", WorkCount = 2, RatingCount = 3, Score = 3.5 }));
        }

        // The isolated view verifies requests without routing synthetic IDs to a database.
        var view = new Yiye.Views.DashboardView();
        Populate(view);
        var requestedWorks = new List<string>();
        view.WorkRequested += requestedWorks.Add;
        var panel = (FrameworkElement)view.FindName("DashboardShowcasePanel");
        foreach (var width in new[] { 1100d, 850d, 600d })
        {
            for (var pass = 0; pass < 2; pass++)
            {
                panel.Measure(new Size(width, double.PositiveInfinity));
                panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
                panel.UpdateLayout();
            }
            var recent = (ItemsControl)view.FindName("RecentWorkPicker");
            Assert.Equal(samples.Length, recent.Items.Count);
            var recentButtons = Descendants(recent).OfType<Button>().Where(button => button.Tag is ShowcaseCard).ToArray();
            Assert.Equal(samples.Select(item => item.WorkId), recentButtons.Select(button => ((ShowcaseCard)button.Tag).WorkId));
            AssertNoOverlap(recentButtons, recent);
            foreach (var button in recentButtons)
            {
                AssertWithin(button, recent);
                Assert.Contains(Descendants(button).OfType<Image>(), image =>
                    image.Source is not null && image.ActualWidth > 0 && image.ActualHeight > 0);
            }
            foreach (var name in new[] { "DashboardBookRanking", "DashboardScreenRanking" })
            {
                var ranking = (FrameworkElement)view.FindName(name);
                var buttons = Descendants(ranking).OfType<Button>().Where(button => button.Tag is ShowcaseCard).ToArray();
                Assert.Equal(3, buttons.Length);
                AssertNoOverlap(buttons, ranking);
                Assert.All(buttons, button => AssertWithin(button, ranking));
            }
            var sections = new[] { "DashboardRecentSection", "DashboardSideColumn", "DashboardJournalSection" }
                .Select(name => (FrameworkElement)view.FindName(name)).ToArray();
            Assert.All(sections, section => AssertWithin(section, panel));
            AssertNoOverlap(sections, panel);
            Assert.Equal(3, ((ItemsControl)view.FindName("DashboardTimelineList")).Items.Count);
            foreach (var button in Descendants(panel).OfType<Button>().Where(button => button.Tag is ShowcaseCard or TimelineCard))
            {
                Assert.True(button.IsEnabled && button.Focusable);
                requestedWorks.Clear();
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var workId = button.Tag switch { ShowcaseCard card => card.WorkId, TimelineCard card => card.WorkId, _ => null };
                Assert.Equal(workId, Assert.Single(requestedWorks));
            }
            SaveSnapshot(panel, $"dashboard-showcase-{width:0}.png");
        }

        Populate(window.HomeView);
        ((FrameworkElement)Find(window, "DashboardHero")).Visibility = Visibility.Collapsed;
        ((FrameworkElement)Find(window, "DashboardCollectionHeader")).Visibility = Visibility.Visible;
        var scroll = (ScrollViewer)Find(window, "DashboardScroll");
        var root = (Grid)window.Content;
        window.Content = null;
        root.DataContext = window;
        root.Resources = window.Resources;
        foreach (var height in new[] { 800d, 768d, 640d, 800d })
        {
            scroll.Visibility = Visibility.Visible;
            root.Measure(new Size(1280, height));
            root.Arrange(new Rect(0, 0, 1280, height));
            root.UpdateLayout();
            AssertScrollAccess(scroll);
            if (height >= 768)
                Assert.InRange(scroll.ScrollableHeight, 0, 1);
            AssertWithin((Button)Find(window, "GlobalAddWorkButton"), root);
            AssertWithin((TextBox)Find(window, "GlobalSearchBox"), root);
        }
        SaveSnapshot(root, "dashboard-full.png");
        window.ShowLibrary();
        root.Measure(new Size(1280, 800));
        root.Arrange(new Rect(0, 0, 1280, 800));
        root.UpdateLayout();
        var add = (Button)Find(window, "GlobalAddWorkButton");
        Assert.True(add.IsEnabled && add.Focusable);
        AssertWithin(add, root);
        AssertWithin((TextBox)Find(window, "GlobalSearchBox"), root);

        // A shown host includes the title bar, matching the real minimum window's viewport.
        window.ShowHome();
        var compactHost = new Window
        {
            Content = root, Width = 980, Height = 640, WindowStyle = window.WindowStyle,
            Style = window.Style, FontFamily = window.FontFamily, FontSize = window.FontSize,
            Foreground = window.Foreground, Background = window.Background,
            UseLayoutRounding = window.UseLayoutRounding, ShowActivated = false, ShowInTaskbar = false
        };
        TextOptions.SetTextFormattingMode(compactHost, TextOptions.GetTextFormattingMode(window));
        InputMethod.SetIsInputMethodEnabled(compactHost, false);
        try
        {
            compactHost.Show();
            SettleAnimations();
            compactHost.UpdateLayout();
            Assert.InRange(scroll.ScrollableHeight, 0, 1);
            foreach (var name in new[] { "DashboardRecentSection", "DashboardSideColumn", "DashboardJournalSection", "DashboardAuthorRanking" })
                AssertWithin((FrameworkElement)Find(window, name), scroll);
            AssertWithin(add, root);
            AssertWithin((TextBox)Find(window, "GlobalSearchBox"), root);
            SaveSnapshot(compactHost, "dashboard-full-compact.png");
        }
        finally { compactHost.Close(); }
    }

    private static void AssertLibraryLayout(TempDatabase context, string coverPath)
    {
        var window = new MainWindow(new LibraryApplication(context.Database));
        window.ShowLibrary();
        var works = Enumerable.Range(0, 12).Select(index => new MediaWork
        {
            Title = index == 11 ? "走夜路请放声歌唱：一段较长的副题" : $"收藏作品 {index:00}",
            Kind = index < 9 ? "book" : "screen", PrimaryCoverPath = coverPath, AggregateRank = 2.4,
            ExperienceCount = 1, LatestActivityOn = new DateOnly(2026, 9, 18)
        }).ToList();
        window.LibraryView.SetWorks(works);
        var list = (ListBox)Find(window, "WorkList");
        var monthOverview = (ScrollViewer)Find(window, "MonthOverview");
        var nextPage = (Button)Find(window, "NextPageButton");
        var previousPage = (Button)Find(window, "PreviousPageButton");
        var root = (Grid)window.Content;
        window.Content = null;
        root.DataContext = window;
        root.Resources = window.Resources;
        double width = 1280, height = 800;
        void Layout()
        {
            for (var pass = 0; pass < 2; pass++)
            {
                root.Measure(new Size(width, height));
                root.Arrange(new Rect(0, 0, width, height));
                root.UpdateLayout();
            }
        }
        void AssertCurrentPage()
        {
            Assert.Equal(window.LibraryView.PageWorks.Select(work => work.Id).Order(),
                list.Items.OfType<WorkCard>().Select(work => work.Id).Order());
            var tiles = Descendants(list).OfType<ListBoxItem>().ToArray();
            Assert.Equal(window.LibraryView.PageWorks.Count, tiles.Length);
            var groups = list.Items.Groups!.Cast<CollectionViewGroup>().Where(group => group.ItemCount > 0).ToArray();
            Assert.Equal(window.LibraryView.PageWorks.Select(work => work.KindLabel).Distinct(), groups.Select(group => group.Name));
            var panels = Descendants(list).OfType<Border>()
                .Where(panel => panel.Name == "ShelfGroupPanel" && panel.ActualHeight > 0).ToArray();
            Assert.Equal(groups.Length, panels.Length);
            AssertNoOverlap(panels, list);
            Assert.All(panels, panel => AssertWithin(panel, list));
            AssertNoOverlap(tiles, list);
            foreach (var tile in tiles)
            {
                AssertWithin(tile, list);
                Assert.True(tile.Focusable);
                Assert.Contains(Descendants(tile).OfType<Image>(), image =>
                    image.Source is not null && image.ActualWidth > 0 && image.ActualHeight > 0);
            }
            var scroll = Descendants(list).OfType<ScrollViewer>().Single();
            AssertScrollAccess(scroll);
            Assert.InRange(scroll.ScrollableHeight, 0, 1);
            AssertWithin((FrameworkElement)Find(window, "LibraryPager"), root);
            AssertWithin(nextPage, root);
            AssertWithin(previousPage, root);
        }
        void AssertEveryWorkIsReachable()
        {
            window.LibraryView.SetPage(0);
            Layout();
            Assert.False(previousPage.IsEnabled);
            var found = new HashSet<string>();
            var pages = window.LibraryView.PageCount;
            for (var page = 0; page < pages; page++)
            {
                Assert.Equal(page, window.LibraryView.PageIndex);
                AssertCurrentPage();
                foreach (var card in window.LibraryView.PageWorks)
                    Assert.True(found.Add(card.Id), "A work should appear once across library pages.");
                Assert.Equal(page + 1 < pages, nextPage.IsEnabled);
                if (page + 1 < pages)
                {
                    Assert.True(nextPage.Focusable);
                    nextPage.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Layout();
                }
            }
            Assert.Equal(works.Select(work => work.Id).Order(), found.Order());
            if (pages > 1)
            {
                Assert.True(previousPage.IsEnabled && previousPage.Focusable);
                previousPage.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Layout();
                Assert.Equal(pages - 2, window.LibraryView.PageIndex);
            }
            window.LibraryView.SetPage(0);
            Layout();
        }
        try
        {
            Layout();
            Assert.False(window.LibraryView.IsMonthView);
            Assert.Equal(Visibility.Visible, list.Visibility);
            Assert.Equal(Visibility.Collapsed, monthOverview.Visibility);
            Assert.Equal(1, window.LibraryView.PageCount);
            Assert.Equal(works.Count, list.Items.Count);
            AssertEveryWorkIsReachable();
            SaveSnapshot(root, "library-grid.png");

            width = 980; height = 640;
            Layout();
            Assert.True(window.LibraryView.PageCount > 1, "A compact window should use fewer works per page.");
            AssertEveryWorkIsReachable();
            SaveSnapshot(root, "library-grid-compact.png");
            width = 1280; height = 800;
            Layout();

            window.LibraryView.SetMonthView(true);
            Layout();
            Assert.Single(window.LibraryView.LibraryMonths);
            Assert.Single(window.LibraryView.PageMonths);
            Assert.Equal(Visibility.Collapsed, list.Visibility);
            Assert.Equal(Visibility.Visible, monthOverview.Visibility);
            Assert.InRange(monthOverview.ScrollableHeight, 0, 1);
            SaveSnapshot(root, "library-months.png");
            var monthButton = Descendants(monthOverview).OfType<Button>().Single(button => button.Name == "MonthDeckButton");
            AssertWithin(monthButton, monthOverview);
            monthButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout();
            Assert.Equal(Visibility.Collapsed, monthOverview.Visibility);
            Assert.Equal(Visibility.Visible, list.Visibility);
            Assert.Equal(works.Count, window.LibraryView.ExpandedMonthWorks.Count);
            AssertEveryWorkIsReachable();

            for (var index = 12; index < 20; index++)
                works.Add(new MediaWork
                {
                    Title = $"收藏作品 {index:00}", Kind = "book", PrimaryCoverPath = coverPath,
                    LatestActivityOn = new DateOnly(2026, 9, 18)
                });
            window.LibraryView.SetWorks(works);
            window.LibraryView.OpenMonth("2026-09");
            Layout();
            Assert.True(window.LibraryView.PageCount > 1);
            Assert.Equal(works.Count, window.LibraryView.ExpandedMonthWorks.Count);
            AssertEveryWorkIsReachable();
            ((Button)Find(window, "MonthBackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout();
            Assert.Equal(Visibility.Visible, monthOverview.Visibility);
            Assert.Equal(Visibility.Collapsed, list.Visibility);
            Assert.Empty(window.LibraryView.ExpandedMonthWorks);
            Assert.Equal(works.Count, Assert.Single(window.LibraryView.LibraryMonths).Works.Count);
            window.LibraryView.SetMonthView(false);
            Layout();
            Assert.Equal(Visibility.Visible, list.Visibility);
            Assert.Equal(0, window.LibraryView.PageIndex);
            AssertEveryWorkIsReachable();

            var datedWorks = Enumerable.Range(0, 18).Select(index => new MediaWork
            {
                Title = $"月份作品 {index}", Kind = "book", PrimaryCoverPath = coverPath,
                LatestActivityOn = new DateOnly(2025, 1, 1).AddMonths(index)
            }).ToArray();
            window.LibraryView.SetWorks(datedWorks);
            window.LibraryView.SetMonthView(true);
            Layout();
            Assert.True(window.LibraryView.PageCount > 1);
            var visitedMonths = new HashSet<string>();
            for (var page = 0; page < window.LibraryView.PageCount; page++)
            {
                Assert.Equal(page, window.LibraryView.PageIndex);
                var decks = Descendants(monthOverview).OfType<Button>().Where(button => button.Name == "MonthDeckButton").ToArray();
                Assert.Equal(window.LibraryView.PageMonths.Count, decks.Length);
                AssertNoOverlap(decks, monthOverview);
                Assert.All(decks, deck => AssertWithin(deck, monthOverview));
                Assert.InRange(monthOverview.ScrollableHeight, 0, 1);
                foreach (var month in window.LibraryView.PageMonths) Assert.True(visitedMonths.Add(month.Key));
                if (page + 1 < window.LibraryView.PageCount)
                {
                    nextPage.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Layout();
                }
            }
            Assert.Equal(window.LibraryView.LibraryMonths.Select(month => month.Key).Order(), visitedMonths.Order());
            var overviewPage = window.LibraryView.PageIndex;
            var lastMonth = window.LibraryView.PageMonths[^1];
            window.LibraryView.OpenMonth(lastMonth.Key);
            Layout();
            Assert.Equal(lastMonth.Key, window.LibraryView.Selection.ExpandedMonthKey);
            ((Button)Find(window, "MonthBackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout();
            Assert.Equal(overviewPage, window.LibraryView.PageIndex);
            Assert.Contains(window.LibraryView.PageMonths, month => month.Key == lastMonth.Key);
            window.ShowHome();
            Assert.Equal(Visibility.Collapsed, window.LibraryView.Visibility);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)Find(window, "NavigationRail")).Visibility);
        }
        finally { window.Close(); }
    }

    private static void AssertMonthFiltering()
    {
        var book = new MediaWork { Title = "September book", Author = "Writer", Kind = "book", LatestActivityOn = new DateOnly(2026, 9, 20) };
        var screen = new MediaWork { Title = "August screen", Kind = "screen", LatestActivityOn = new DateOnly(2026, 8, 20) };
        var undated = new MediaWork { Title = "Unrecorded", Kind = "book" };
        var view = new Yiye.Views.LibraryView();
        var list = (ListBox)view.FindName("WorkList");
        view.SetWorks([book, screen, undated]);
        view.Measure(new Size(1160, 680));
        view.Arrange(new Rect(0, 0, 1160, 680));
        view.UpdateLayout();
        Assert.Equal(3, view.LibraryMonths.Count);
        Assert.False(view.IsMonthView);
        Assert.Equal(3, list.Items.Count);
        Assert.Equal(Visibility.Visible, list.Visibility);
        view.OpenMonth(view.LibraryMonths[0].Key);
        Assert.True(view.IsMonthView);
        Assert.Equal(book.Id, Assert.Single(view.ExpandedMonthWorks).Id);
        view.SetFilter("all", "Writer");
        Assert.Equal(book.Id, Assert.Single(Assert.Single(view.LibraryMonths).Works).Id);
        Assert.Empty(view.ExpandedMonthWorks);
        Assert.Equal(Visibility.Visible, ((ScrollViewer)view.FindName("MonthOverview")).Visibility);
        view.SetFilter("screen", "");
        Assert.Equal(screen.Id, Assert.Single(Assert.Single(view.LibraryMonths).Works).Id);
        view.SetMonthView(false);
        Assert.Equal(screen.Id, Assert.IsType<WorkCard>(Assert.Single(list.Items.Cast<object>())).Id);
        Assert.Equal("影视", Assert.Single(list.Items.Groups!.Cast<CollectionViewGroup>(), group => group.ItemCount > 0).Name);
        string? requested = null;
        view.WorkRequested += id => requested = id;
        list.SelectedItem = view.VisibleWorks[0];
        Assert.Equal(screen.Id, requested);
        Assert.Null(list.SelectedItem);
        view.SetFilter("screen", "missing");
        Assert.Empty(view.LibraryMonths);
        Assert.Empty(view.ExpandedMonthWorks);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)view.FindName("EmptyState")).Visibility);
        Assert.Equal(Visibility.Collapsed, ((FrameworkElement)view.FindName("MonthOverview")).Visibility);
        Assert.Equal(Visibility.Collapsed, list.Visibility);
    }

    private static void AssertDetailLayout(TempDatabase context, string coverPath)
    {
        var window = new MainWindow(new LibraryApplication(context.Database));
        window.DetailView.Visibility = Visibility.Visible;
        window.HomeView.Visibility = Visibility.Collapsed;
        ((ScrollViewer)Find(window, "DashboardScroll")).Visibility = Visibility.Collapsed;
        var work = new MediaWork
        {
            Id = "detail-layout", Title = "真事隐", Subtitle = "康熙废储与正史虚构", Author = "孙立天", Kind = "book",
            ExperienceCount = 1, RatedExperienceCount = 1, AggregateRank = 3.5, LatestActivityOn = new DateOnly(2026, 9, 13)
        };
        var covers = new[] { new WorkCover { WorkId = work.Id, FileName = Path.GetFileName(coverPath), FilePath = coverPath } };
        var experiences = Enumerable.Range(1, 8).Select(index => new MediaExperience
        {
            WorkId = work.Id, CompletedOn = new DateOnly(2026, 9, index),
            Allure = 3, Immersion = 4, Rationality = 5, Illumination = 4,
            Notes = $"第 {index} 次阅读的记录。这里保留当时记下的内容，之后仍可以打开查看和编辑。"
        }).ToArray();
        window.DetailView.Render(new WorkDetail(work, covers, experiences.Take(1).ToArray()));
        var scroll = (ScrollViewer)Find(window, "DetailScroll");
        var root = (Grid)window.Content;
        window.Content = null;
        root.DataContext = window;
        root.Resources = window.Resources;
        void Layout(double height)
        {
            root.Measure(new Size(1280, height));
            root.Arrange(new Rect(0, 0, 1280, height));
            root.UpdateLayout();
        }
        foreach (var height in new[] { 800d, 640d, 800d })
        {
            Layout(height);
            AssertDetailActions(window);
            AssertScrollAccess(scroll);
        }
        SaveSnapshot(root, "detail-single-record.png");
        window.DetailView.Render(new WorkDetail(work, covers, experiences));
        var host = new Window { Content = root, Width = 1280, Height = 800, WindowStyle = WindowStyle.None, ShowActivated = false };
        InputMethod.SetIsInputMethodEnabled(host, false);
        try
        {
            host.Show();
            host.UpdateLayout();
            Assert.Equal(experiences.Length, window.DetailView.CompletedExperiences.Count);
            var history = (ListBox)Find(window, "HistoryList");
            Assert.All(Descendants(history).OfType<ScrollViewer>(), inner =>
                Assert.InRange(inner.ScrollableHeight, 0, 1));
            Assert.True(scroll.ScrollableHeight > 0, "Long records should expand into the page's scroll area.");
            history.ScrollIntoView(window.DetailView.CompletedExperiences[^1]);
            host.UpdateLayout();
            var last = Assert.IsAssignableFrom<FrameworkElement>(history.ItemContainerGenerator.ContainerFromIndex(experiences.Length - 1));
            AssertWithin(last, history, vertical: false);
            var edit = Assert.Single(Descendants(last).OfType<Button>());
            edit.BringIntoView();
            var frame = new DispatcherFrame();
            host.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            host.UpdateLayout();
            Assert.True(edit.IsEnabled && edit.Focusable);
            AssertWithin(edit, scroll);
            AssertScrollAccess(scroll);
        }
        finally { host.Close(); window.Close(); }
    }

    private static void SnapshotWindow(Window window, string fileName)
    {
        window.Show();
        window.UpdateLayout();
        SaveSnapshot(window, fileName);
        window.Hide();
    }

    private static void AssertDetailActions(MainWindow window)
    {
        var scroll = (ScrollViewer)Find(window, "DetailScroll");
        scroll.ScrollToTop();
        scroll.UpdateLayout();
        var cover = (Button)Find(window, "DetailCoverButton");
        var information = (FrameworkElement)Find(window, "CoverPlate");
        var records = (FrameworkElement)Find(window, "DetailRecordsPanel");
        Assert.NotNull(window.DetailView.CoverImageSource);
        AssertWithin(information, scroll, vertical: false);
        AssertWithin(records, scroll, vertical: false);
        AssertNoOverlap([information, records], scroll);
        AssertWithin((TextBlock)Find(window, "DetailTitleText"), information);
        AssertWithin((FrameworkElement)Find(window, "HistoryHeader"), records);
        AssertWithin(cover, scroll, vertical: false);
        Assert.True(cover.IsEnabled && cover.Focusable);
        var actions = (FrameworkElement)Find(window, "HeroActions");
        var buttons = Descendants(actions).OfType<Button>().ToArray();
        Assert.NotEmpty(buttons);
        AssertNoOverlap(buttons, actions);
        foreach (var button in buttons)
        {
            Assert.True(button.IsEnabled && button.Focusable);
            AssertWithin(button, actions);
            button.BringIntoView();
            scroll.UpdateLayout();
            AssertWithin(button, scroll);
        }
        scroll.ScrollToTop();
        scroll.UpdateLayout();
    }

    private static void AssertScrollAccess(ScrollViewer scroll)
    {
        Assert.True(scroll.ViewportWidth > 0 && scroll.ViewportHeight > 0, "The content viewport must be visible.");
        Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1,
            $"Content must fit horizontally: extent={scroll.ExtentWidth}, viewport={scroll.ViewportWidth}.");
        scroll.ScrollToBottom();
        scroll.UpdateLayout();
        Assert.InRange(Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight), 0, 1);
        scroll.ScrollToTop();
        scroll.UpdateLayout();
        Assert.InRange(scroll.VerticalOffset, 0, 1);
    }

    private static void AssertWithin(FrameworkElement element, FrameworkElement container, bool vertical = true)
    {
        Assert.True(element.ActualWidth > 0 && element.ActualHeight > 0, $"{element.Name} must be rendered.");
        var bounds = element.TransformToAncestor(container).TransformBounds(new Rect(element.RenderSize));
        Assert.True(bounds.Left >= -0.5 && bounds.Right <= container.ActualWidth + 0.5,
            $"{element.Name} exceeds horizontal bounds: {bounds}, container width={container.ActualWidth}.");
        if (vertical)
            Assert.True(bounds.Top >= -0.5 && bounds.Bottom <= container.ActualHeight + 0.5,
                $"{element.Name} must be reachable within the viewport: {bounds}, container height={container.ActualHeight}.");
    }

    private static void AssertNoOverlap(IEnumerable<FrameworkElement> elements, FrameworkElement container)
    {
        var bounds = elements.Select(element => element.TransformToAncestor(container)
            .TransformBounds(new Rect(element.RenderSize))).ToArray();
        for (var first = 0; first < bounds.Length; first++)
            for (var second = first + 1; second < bounds.Length; second++)
            {
                var intersection = Rect.Intersect(bounds[first], bounds[second]);
                Assert.True(intersection.IsEmpty || intersection.Width <= 0.5 || intersection.Height <= 0.5,
                    $"Items {first} and {second} overlap: {bounds[first]} and {bounds[second]}.");
            }
    }

    private static void AssertTimelineTemplate(MainWindow mainWindow)
    {
        var timeline = Assert.IsType<ItemsControl>(Find(mainWindow, "DashboardTimelineList"));
        var presenter = new ContentPresenter
        {
            ContentTemplate = timeline.ItemTemplate,
            Resources = mainWindow.HomeView.Resources,
            Width = 280,
            Content = new DashboardTimelineDay
            {
                Date = new DateOnly(2026, 8, 28),
                Items = [new TimelineCard(new DashboardTimelineItem
                {
                    Id = "completion", WorkId = "timeline-test", Title = "一本完成的书", Kind = "book",
                    Notes = "合上书之后，仍然记得这一段旅程。"
                })]
            }
        };
        presenter.Measure(new Size(presenter.Width, double.PositiveInfinity));
        presenter.Arrange(new Rect(presenter.DesiredSize));
        presenter.UpdateLayout();
        var buttons = Descendants(presenter).OfType<Button>().ToArray();
        Assert.Single(buttons);
        Assert.All(buttons, button =>
        {
            Assert.IsType<TimelineCard>(button.Tag);
            AssertWithin(button, presenter);
            Assert.True(button.IsEnabled && button.Focusable);
        });
        SaveSnapshot(presenter, "timeline-day.png");
    }

    private static object Find(Window window, string name) => window is MainWindow main
        ? FindMain(main, name) : window.FindName(name) ?? throw new InvalidOperationException($"Missing control {name}");

    private static object FindMain(MainWindow window, string name) => window.FindName(name)
        ?? window.HomeView.FindName(name) ?? window.LibraryView.FindName(name) ?? window.DetailView.FindName(name)
        ?? throw new InvalidOperationException($"Missing control {name}");

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void AssertExperienceRatingTemplate(MainWindow mainWindow)
    {
        var history = Assert.IsType<ListBox>(Find(mainWindow, "HistoryList"));
        var presenter = new ContentPresenter
        {
            ContentTemplate = history.ItemTemplate,
            Resources = mainWindow.HomeView.Resources,
            Width = 880
        };

        void Display(MediaExperience experience)
        {
            presenter.Content = new ExperienceArchiveCard { ArchiveNumber = 1, Experience = experience };
            presenter.Measure(new Size(presenter.Width, double.PositiveInfinity));
            presenter.Arrange(new Rect(presenter.DesiredSize));
            presenter.UpdateLayout();
        }

        Grid Scores() => Assert.IsType<Grid>(history.ItemTemplate.FindName("DimensionScores", presenter));
        TextBlock IncompleteMessage() => Assert.IsType<TextBlock>(history.ItemTemplate.FindName("IncompleteRating", presenter));
        string[] Values() => Scores().Children.OfType<StackPanel>()
            .SelectMany(panel => panel.Children.OfType<TextBlock>())
            .Select(text => new TextRange(text.ContentStart, text.ContentEnd).Text).ToArray();

        Display(new MediaExperience
        {
            WorkId = "ui-rating",
            StartedOn = new DateOnly(2026, 8, 26),
            CompletedOn = new DateOnly(2026, 8, 28),
            ProgressEntryCount = 3,
            Allure = 3,
            Immersion = 5,
            Rationality = 4,
            Illumination = 4,
            Notes = "一段需要在较窄卡片中换行展示的完成总结，评分与日期也应完整留在卡片内。"
        });
        Assert.Equal(Visibility.Visible, Scores().Visibility);
        Assert.Equal(Visibility.Collapsed, IncompleteMessage().Visibility);
        Assert.Equal(["Allure", "3 / 3", "Immersion", "5 / 5", "Rationality", "4 / 5", "Illumination", "4 / 5"], Values());
        SaveSnapshot(presenter, "experience-ratings.png");

        presenter.Width = 650;
        presenter.Measure(new Size(presenter.Width, double.PositiveInfinity));
        presenter.Arrange(new Rect(presenter.DesiredSize));
        presenter.UpdateLayout();
        foreach (var name in new[] { "ArchiveTicket", "ArchiveNotes", "DimensionScores" })
        {
            var section = Assert.IsType<Grid>(history.ItemTemplate.FindName(name, presenter));
            Assert.True(section.ActualWidth > 0 && section.ActualHeight > 0, $"{name} must be rendered.");
            var bounds = section.TransformToAncestor(presenter).TransformBounds(new Rect(section.RenderSize));
            Assert.True(bounds.Left >= -0.5 && bounds.Right <= presenter.ActualWidth + 0.5,
                $"{name} overflows the archive card: bounds={bounds}, card width={presenter.ActualWidth}");
        }
        SaveSnapshot(presenter, "experience-ratings-narrow.png");

        Display(new MediaExperience { WorkId = "ui-rating", Allure = 3 });
        Assert.Equal(Visibility.Collapsed, Scores().Visibility);
        Assert.Equal(Visibility.Visible, IncompleteMessage().Visibility);

        Display(new MediaExperience { WorkId = "ui-rating", Allure = 1, Immersion = 2, Rationality = 3, Illumination = 4 });
        Assert.Equal(Visibility.Visible, Scores().Visibility);
        Assert.Equal(Visibility.Collapsed, IncompleteMessage().Visibility);
        Assert.Equal(["Allure", "1 / 3", "Immersion", "2 / 5", "Rationality", "3 / 5", "Illumination", "4 / 5"], Values());
    }

    private static void SaveSnapshot(FrameworkElement element, string fileName)
    {
        var directory = Environment.GetEnvironmentVariable("QUIETSHELF_UI_SNAPSHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(element.ActualWidth * 2),
            (int)Math.Ceiling(element.ActualHeight * 2),
            192, 192, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, fileName));
        encoder.Save(stream);
    }
}
