using Yiye.Operations;
using Yiye.Presentation;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
    public async Task CoreWindowsCanBeConstructedOnStaThread()
    {
        await using var context = await TempDatabase.CreateAsync();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new Yiye.App(launchWindow: false);
                application.InitializeComponent();
                var mainWindow = new MainWindow();
                Assert.IsAssignableFrom<TextBlock>(Find(mainWindow, "LibraryCountText"));
                Assert.IsAssignableFrom<ScrollViewer>(Find(mainWindow, "DashboardScroll"));
                Assert.IsAssignableFrom<FrameworkElement>(Find(mainWindow, "DashboardTimelineList"));
                Assert.IsAssignableFrom<TextBlock>(Find(mainWindow, "DashboardWorkCountText"));
                Assert.IsAssignableFrom<Button>(Find(mainWindow, "HomeButton"));
                Assert.IsAssignableFrom<FrameworkElement>(Find(mainWindow, "RegularWorksHeader"));
                Assert.IsAssignableFrom<TextBlock>(Find(mainWindow, "DetailKickerText"));
                Assert.IsType<StackPanel>(Find(mainWindow, "DashboardShowcasePanel"));
                Assert.IsType<Border>(Find(mainWindow, "DetailHeroShell"));
                AssertExperienceRatingTemplate(mainWindow);
                AssertTimelineTemplate(mainWindow);
                AssertDashboardLayout(mainWindow);
                AssertDetailLayout();
                AssertLibraryLayout();
                AssertMonthFiltering();
                var addWork = new AddWorkWindow();
                Assert.IsType<Border>(addWork.FindName("WorkFormSection"));
                var addExperience = new AddExperienceWindow("ui-test", "book");
                Assert.IsType<Border>(addExperience.FindName("ExperienceDateSection"));
                Assert.IsType<Border>(addExperience.FindName("RatingSection"));
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
                AssertRatingPopupOpening(editExperience, editing: true);
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
                AssertRatingPopupOpening(addExperience, editing: false);

                AssertExplicitWorkSelection(context);
                AssertCoverLoadFailure(context, application);

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

    private static void AssertRatingPopupOpening(AddExperienceWindow window, bool editing)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = SystemParameters.WorkArea.Left + 30;
        window.Top = Math.Max(SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom - window.Height);
        window.Show();
        window.Activate();
        window.UpdateLayout();
        foreach (var name in new[] { "AllureBox", "ImmersionBox", "RationalityBox", "IlluminationBox" })
        {
            var box = Assert.IsType<ComboBox>(Find(window, name));
            box.BringIntoView();
            window.UpdateLayout();
            box.Focus();
            var expected = editing && name == "IlluminationBox" ? 4 : 0;
            Assert.Equal(expected, box.SelectedIndex);
            var toggle = Assert.IsType<System.Windows.Controls.Primitives.ToggleButton>(box.Template.FindName("ToggleButton", box));
            Assert.Equal(ClickMode.Release, toggle.ClickMode);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                box.IsDropDownOpen = true;
                Assert.True(box.IsDropDownOpen, $"{name}: loaded={box.IsLoaded}, visible={box.IsVisible}, enabled={box.IsEnabled}, items={box.Items.Count}, toggle={toggle.IsChecked}, active={window.IsActive}, capture={Mouse.Captured}, source={PresentationSource.FromVisual(box)}");
                window.UpdateLayout();
                Assert.True(box.IsDropDownOpen);
                Assert.Equal(expected, box.SelectedIndex);
                box.IsDropDownOpen = false;
            }
            box.SelectedIndex = 2;
            box.IsDropDownOpen = true;
            window.UpdateLayout();
            Assert.Equal(2, box.SelectedIndex);
            box.IsDropDownOpen = false;
            box.SelectedIndex = expected;
        }
        window.Hide();
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

    private static void AssertDashboardLayout(MainWindow window)
    {
        for (var index = 1; index <= 3; index++)
        {
            var work = new DashboardShowcaseItem
            {
                WorkId = index.ToString(),
                Title = $"收藏作品 {index}",
                Author = "示例作者",
                AggregateRank = 3.5,
                RatingCount = 2,
                CompletionCount = 2,
                FirstCompletedOn = new DateOnly(2026, 1, index),
                LatestCompletedOn = new DateOnly(2026, 8, index)
            };
            window.HomeView.DashboardTopBooks.Add(new ShowcaseCard(work));
            if (index == 1) window.HomeView.DashboardTopScreens.Add(new ShowcaseCard(work));
            window.HomeView.DashboardRecentWorks.Add(new ShowcaseCard(work));
            window.HomeView.DashboardTimelineDays.Add(new DashboardTimelineDay
            {
                Date = work.LatestCompletedOn,
                Items = [new TimelineCard(new DashboardTimelineItem { Id = $"event-{index}", WorkId = work.WorkId,
                    Title = work.Title, Kind = "book" })]
            });
            window.HomeView.DashboardTopAuthors.Add(new AuthorCard(new DashboardAuthorRank
            {
                Position = index,
                Author = $"作者 {index}",
                WorkCount = 2,
                RatingCount = 3,
                WeightedRank = 3.5
            }));
        }
        var panel = (StackPanel)Find(window, "DashboardShowcasePanel");
        foreach (var width in new[] { 1100d, 850d, 600d })
        {
            panel.Measure(new Size(width, double.PositiveInfinity));
            panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
            panel.UpdateLayout();
            panel.Measure(new Size(width, double.PositiveInfinity));
            panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
            panel.UpdateLayout();
            foreach (var wrap in Descendants(panel).OfType<WrapPanel>())
            {
                foreach (FrameworkElement child in wrap.Children)
                {
                    var origin = child.TranslatePoint(new Point(), wrap);
                    Assert.True(origin.X + child.ActualWidth <= wrap.ActualWidth + 0.5);
                }
            }
            if (width >= 700) Assert.Equal(((Border)Find(window, "DashboardScreenRanking")).ActualHeight,
                        ((Border)Find(window, "DashboardBookRanking")).ActualHeight);
            var sideColumn = (Grid)Find(window, "DashboardSideColumn");
            Assert.Equal(1, Grid.GetRow(sideColumn));
            Assert.Equal(width < 700 ? 1 : 0, Grid.GetRow((Border)Find(window, "DashboardScreenRanking")));
            foreach (var list in Descendants(panel).OfType<ItemsControl>()
                         .Where(list => ReferenceEquals(list.ItemTemplate, window.HomeView.FindResource("ShowcaseRankItemTemplate"))))
            {
                Assert.All(Descendants(list).OfType<Button>(), button =>
                    Assert.True(button.ActualWidth >= list.ActualWidth - 1, "Rank rows should fill the list width."));
            }
            var journal = (Border)Find(window, "DashboardJournalSection");
            var rankingsBottom = sideColumn.TranslatePoint(new Point(0, sideColumn.ActualHeight), panel).Y;
            if (width < 950) Assert.True(journal.TranslatePoint(new Point(), panel).Y >= rankingsBottom);
            else Assert.Equal(2, Grid.GetColumn(journal));
            Assert.Equal(3, sideColumn.Children.Count);
            var picker = (ListBox)Find(window, "RecentWorkPicker");
            picker.SelectedIndex = 1;
            panel.UpdateLayout();
            Assert.Same(window.HomeView.DashboardRecentWorks[1], picker.SelectedItem);
            Assert.Contains(Descendants(panel).OfType<ContentControl>(), control => ReferenceEquals(control.Content, picker.SelectedItem));
            SaveSnapshot(panel, $"dashboard-showcase-{width:0}.png");
        }
        ((Border)Find(window, "DashboardHero")).Visibility = Visibility.Collapsed;
        ((Grid)Find(window, "DashboardCollectionHeader")).Visibility = Visibility.Visible;
        var scroll = (ScrollViewer)Find(window, "DashboardScroll");
        var root = (Grid)window.Content;
        window.Content = null;
        root.DataContext = window;
        root.Resources = window.Resources;
        window.HomeView.DashboardTopBooks.RemoveAt(2);
        window.HomeView.DashboardTopAuthors.RemoveAt(2);
        window.HomeView.DashboardTopScreens.Add(window.HomeView.DashboardTopBooks[1]);
        window.HomeView.DashboardTimelineDays.Add(window.HomeView.DashboardTimelineDays[0]);
        foreach (var height in new[] { 800d, 768d, 640d, 800d })
        {
            scroll.Visibility = Visibility.Visible;
            root.Measure(new Size(1280, height));
            root.Arrange(new Rect(0, 0, 1280, height));
            root.UpdateLayout();
            Assert.True(height < 700 || scroll.ScrollableHeight == 0, $"height={height}, extent={scroll.ExtentHeight}, viewport={scroll.ViewportHeight}, desired={scroll.DesiredSize}");
        }
        SaveSnapshot(root, "dashboard-full.png");
        var dashboardAdd = (Button)Find(window, "DashboardAddWorkButton");
        var dashboardBounds = new Rect(dashboardAdd.TranslatePoint(new Point(), root), dashboardAdd.RenderSize);
        window.ShowLibrary();
        root.Measure(new Size(1280, 800));
        root.Arrange(new Rect(0, 0, 1280, 800));
        root.UpdateLayout();
        var libraryAdd = (Button)Find(window, "LibraryAddWorkButton");
        Assert.Equal(dashboardBounds, new Rect(libraryAdd.TranslatePoint(new Point(), root), libraryAdd.RenderSize));
    }

    private static void AssertLibraryLayout()
    {
        var window = new MainWindow();
        window.ShowLibrary();
        Assert.Equal(Visibility.Visible, ((FrameworkElement)Find(window, "LibraryPane")).Visibility);
        ((FrameworkElement)Find(window, "EmptyState")).Visibility = Visibility.Collapsed;
        var list = (ListBox)Find(window, "WorkList");
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
        Layout(800);
        for (var index = 0; index < 6; index++)
            window.LibraryView.VisibleWorks.Add(new WorkCard(new MediaWork
            {
                Title = "走夜路请放声歌唱",
                Kind = "book",
                AggregateRank = 2.4,
                ExperienceCount = 1,
                LatestActivityOn = new DateOnly(2026, 9, 18)
            }));
        window.LibraryView.RefreshMonths();
        Layout(768);
        Assert.Single(window.LibraryView.LibraryMonths);
        Assert.Equal(Visibility.Collapsed, list.Visibility);
        var monthOverview = (ScrollViewer)Find(window, "MonthOverview");
        Assert.Equal(Visibility.Visible, monthOverview.Visibility);
        SaveSnapshot(root, "library-months.png");
        var monthButton = Descendants(monthOverview).OfType<Button>().Single(button => button.Name == "MonthDeckButton");
        monthButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Layout(768);
        Assert.Equal(Visibility.Collapsed, monthOverview.Visibility);
        Assert.Equal(Visibility.Visible, list.Visibility);
        Assert.Equal(6, window.LibraryView.ExpandedMonthWorks.Count);
        var scroll = Descendants(list).OfType<ScrollViewer>().First();
        SaveSnapshot(root, "library-grid.png");
        var tiles = Descendants(list).OfType<ListBoxItem>().ToArray();
        Assert.Equal(6, tiles.Length);
        var firstTileY = tiles[0].TranslatePoint(new Point(), list).Y;
        Assert.All(tiles, tile =>
        {
            var origin = tile.TranslatePoint(new Point(), list);
            Assert.InRange(Math.Abs(origin.Y - firstTileY), 0, 0.5);
            Assert.True(origin.X + tile.ActualWidth <= list.ActualWidth);
        });
        Assert.True(scroll.ScrollableHeight == 0,
            $"List={list.ActualHeight}, extent={scroll.ExtentHeight}, viewport={scroll.ViewportHeight}, rows={string.Join(',', Descendants(list).OfType<ListBoxItem>().Select(item => item.ActualHeight))}");
        Assert.Equal(Visibility.Collapsed, scroll.ComputedVerticalScrollBarVisibility);
        var stationaryWheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
        { RoutedEvent = Mouse.PreviewMouseWheelEvent };
        list.RaiseEvent(stationaryWheel);
        root.UpdateLayout();
        Assert.True(stationaryWheel.Handled);
        Assert.Equal(0, scroll.VerticalOffset);
        root.Measure(new Size(980, 768));
        root.Arrange(new Rect(0, 0, 980, 768));
        root.UpdateLayout();
        Assert.True(tiles[^1].TranslatePoint(new Point(), list).Y > tiles[0].TranslatePoint(new Point(), list).Y);
        Assert.All(tiles, tile => Assert.True(tile.TranslatePoint(new Point(), list).X + tile.ActualWidth <= list.ActualWidth));
        Layout(768);
        var host = new Window { Content = root, Width = 1280, Height = 800, WindowStyle = WindowStyle.None, ShowActivated = false };
        InputMethod.SetIsInputMethodEnabled(host, false);
        host.Show();
        host.UpdateLayout();
        for (var index = 6; index < 20; index++)
            window.LibraryView.VisibleWorks.Add(new WorkCard(new MediaWork { Title = $"作品 {index}", Kind = "book", LatestActivityOn = new DateOnly(2026, 9, 18) }));
        window.LibraryView.RefreshMonths();
        Layout(800);
        Assert.True(scroll.ScrollableHeight > 0, $"items={list.Items.Count}, extent={scroll.ExtentHeight}, viewport={scroll.ViewportHeight}, width={scroll.ExtentWidth}");
        Assert.Equal(Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility);
        list.ScrollIntoView(window.LibraryView.ExpandedMonthWorks[^1]);
        root.UpdateLayout();
        Assert.True(scroll.VerticalOffset > 0);
        Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(19));
        ((Button)Find(window, "MonthBackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Layout(800);
        Assert.Equal(Visibility.Visible, monthOverview.Visibility);
        Assert.Equal(Visibility.Collapsed, list.Visibility);
        Assert.Empty(window.LibraryView.ExpandedMonthWorks);
        Assert.Equal(20, Assert.Single(window.LibraryView.LibraryMonths).Works.Count);
        window.ShowHome();
        Assert.Equal(Visibility.Collapsed, window.LibraryView.Visibility);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)Find(window, "NavigationRail")).Visibility);

        host.Close();
        window.Close();
    }

    private static void AssertMonthFiltering()
    {
        var window = new MainWindow();
        window.ShowLibrary();
        var book = new MediaWork { Title = "September book", Author = "Writer", Kind = "book", LatestActivityOn = new DateOnly(2026, 9, 20) };
        var screen = new MediaWork { Title = "August screen", Kind = "screen", LatestActivityOn = new DateOnly(2026, 8, 20) };
        var undated = new MediaWork { Title = "Unrecorded", Kind = "book" };
        var view = window.LibraryView;
        view.SetWorks([book, screen, undated]);
        Assert.Equal(3, view.LibraryMonths.Count);
        view.OpenMonth(view.LibraryMonths[0].Key);
        Assert.Same(book, Assert.Single(view.ExpandedMonthWorks).Value);
        view.SetFilter("all", "Writer");
        Assert.Same(book, Assert.Single(Assert.Single(view.LibraryMonths).Works).Value);
        Assert.Empty(view.ExpandedMonthWorks);
        Assert.Equal(Visibility.Visible, ((ScrollViewer)view.FindName("MonthOverview")).Visibility);
        view.SetFilter("screen", "");
        Assert.Same(screen, Assert.Single(Assert.Single(view.LibraryMonths).Works).Value);
        view.SetFilter("screen", "missing");
        Assert.Empty(view.LibraryMonths);
        Assert.Empty(view.ExpandedMonthWorks);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)view.FindName("EmptyState")).Visibility);
        Assert.Equal(Visibility.Collapsed, ((FrameworkElement)view.FindName("MonthOverview")).Visibility);
        Assert.Equal(Visibility.Collapsed, ((FrameworkElement)view.FindName("WorkList")).Visibility);
        window.Close();
    }

    private static void AssertDetailLayout()
    {
        var window = new MainWindow();
        window.DetailView.Visibility = Visibility.Visible;
        ((ScrollViewer)Find(window, "DashboardScroll")).Visibility = Visibility.Collapsed;
        ((FrameworkElement)Find(window, "DetailEmpty")).Visibility = Visibility.Collapsed;
        ((FrameworkElement)Find(window, "HistoryEmpty")).Visibility = Visibility.Collapsed;
        var scroll = (ScrollViewer)Find(window, "DetailScroll");
        scroll.Visibility = Visibility.Visible;
        foreach (var (name, text) in new[]
        {
            ("DetailTitleText", "真事隐"), ("DetailSubtitleText", "康熙废储与正史虚构"),
            ("DetailAuthorText", "孙立天"), ("DetailMetaText", "书籍 · 已记录 1 次 · 最近 2026-09-13"),
            ("DetailRankText", "3.5 / 3.9"), ("DetailCountText", "阅读 1 次"),
            ("DetailRatingCountText", "来自 1 次评分"), ("HistoryCaptionText", "共 1 次")
        })
        {
            var label = (TextBlock)Find(window, name);
            label.Text = text;
            label.Visibility = Visibility.Visible;
        }
        window.DetailView.CompletedExperiences.Add(new ExperienceArchiveCard
        {
            ArchiveNumber = 1,
            Experience = new MediaExperience
            {
                WorkId = "detail-layout",
                CompletedOn = new DateOnly(2026, 9, 13),
                Allure = 3,
                Immersion = 4,
                Rationality = 5,
                Illumination = 4
            }
        });
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
            Assert.True(height < 700 || scroll.ScrollableHeight == 0,
                $"Detail height={height}, extent={scroll.ExtentHeight}, viewport={scroll.ViewportHeight}");
        }
        SaveSnapshot(root, "detail-single-record.png");
        window.DetailView.CompletedExperiences.Add(window.DetailView.CompletedExperiences[0]);
        Layout(800);
        Assert.True(scroll.ScrollableHeight > 0, "Multiple completion records remain scrollable.");
        window.Close();
    }

    private static void SnapshotWindow(Window window, string fileName)
    {
        window.Show();
        window.UpdateLayout();
        SaveSnapshot(window, fileName);
        window.Hide();
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
            Assert.True(button.ActualWidth > 180, "Timeline actions should fill the date card.");
            Assert.True(button.Focusable);
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
            Illumination = 4
        });
        Assert.Equal(Visibility.Visible, Scores().Visibility);
        Assert.Equal(Visibility.Collapsed, IncompleteMessage().Visibility);
        Assert.Equal(["Allure", "3 / 3", "Immersion", "5 / 5", "Rationality", "4 / 5", "Illumination", "4 / 5"], Values());
        SaveSnapshot(presenter, "experience-ratings.png");

        presenter.Width = 650;
        presenter.Measure(new Size(presenter.Width, double.PositiveInfinity));
        presenter.Arrange(new Rect(presenter.DesiredSize));
        presenter.UpdateLayout();
        Assert.True(presenter.DesiredSize.Width <= presenter.Width + 0.5, "Archive cards should fit the available detail width.");
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
