using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuietShelf.Models;

namespace QuietShelf.Tests;

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
                var application = new QuietShelf.App();
                application.InitializeComponent();
                var mainWindow = new MainWindow();
                Assert.IsAssignableFrom<TextBlock>(mainWindow.FindName("LibraryCountText"));
                Assert.IsAssignableFrom<ScrollViewer>(mainWindow.FindName("DashboardScroll"));
                Assert.IsAssignableFrom<FrameworkElement>(mainWindow.FindName("DashboardTimelineList"));
                Assert.IsAssignableFrom<TextBlock>(mainWindow.FindName("DashboardWorkCountText"));
                Assert.IsAssignableFrom<Button>(mainWindow.FindName("HomeButton"));
                Assert.IsAssignableFrom<FrameworkElement>(mainWindow.FindName("RegularWorksHeader"));
                Assert.IsAssignableFrom<TextBlock>(mainWindow.FindName("DetailKickerText"));
                Assert.IsType<StackPanel>(mainWindow.FindName("DashboardShowcasePanel"));
                Assert.IsType<Border>(mainWindow.FindName("DetailHeroShell"));
                AssertExperienceRatingTemplate(mainWindow);
                AssertTimelineTemplate(mainWindow);
                AssertDashboardLayout(mainWindow);
                AssertDetailLayout();
                AssertLibraryLayout();
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
                    CompletedOn = new DateOnly(2026, 8, 29)
                });
                Assert.Equal(Visibility.Visible, Assert.IsAssignableFrom<Button>(editExperience.FindName("DeleteButton")).Visibility);
                editExperience.Close();

                var covers = new ManageCoversWindow(context.Repository, new QuietShelf.Models.MediaWork
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

    private static void AssertDashboardLayout(MainWindow window)
    {
        for (var index = 1; index <= 3; index++)
        {
            var work = new DashboardShowcaseItem
            {
                WorkId = index.ToString(), Title = $"收藏作品 {index}", Author = "示例作者",
                AggregateRank = 3.5, RatingCount = 2, CompletionCount = 2,
                FirstCompletedOn = new DateOnly(2026, 1, index), LatestCompletedOn = new DateOnly(2026, 8, index)
            };
            window.DashboardTopBooks.Add(work);
            if (index == 1) window.DashboardTopScreens.Add(work);
            window.DashboardRecentWorks.Add(work);
            window.DashboardTimelineDays.Add(new DashboardTimelineDay
            {
                Date = work.LatestCompletedOn,
                Items = [new DashboardTimelineItem { Id = $"event-{index}", WorkId = work.WorkId,
                    Title = work.Title, Kind = "book" }]
            });
            window.DashboardTopAuthors.Add(new DashboardAuthorRank
            {
                Position = index, Author = $"作者 {index}", WorkCount = 2, RatingCount = 3, WeightedRank = 3.5
            });
        }
        var panel = (StackPanel)window.FindName("DashboardShowcasePanel");
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
            if (width >= 700) Assert.Equal(((Border)window.FindName("DashboardScreenRanking")).ActualHeight,
                        ((Border)window.FindName("DashboardBookRanking")).ActualHeight);
            var sideColumn = (Grid)window.FindName("DashboardSideColumn");
            Assert.Equal(1, Grid.GetRow(sideColumn));
            Assert.Equal(width < 700 ? 1 : 0, Grid.GetRow((Border)window.FindName("DashboardScreenRanking")));
            foreach (var list in Descendants(panel).OfType<ItemsControl>()
                         .Where(list => ReferenceEquals(list.ItemTemplate, window.Resources["ShowcaseRankItemTemplate"])))
            {
                Assert.All(Descendants(list).OfType<Button>(), button =>
                    Assert.True(button.ActualWidth >= list.ActualWidth - 1, "Rank rows should fill the list width."));
            }
            var journal = (Border)window.FindName("DashboardJournalSection");
            var rankingsBottom = sideColumn.TranslatePoint(new Point(0, sideColumn.ActualHeight), panel).Y;
            if (width < 950) Assert.True(journal.TranslatePoint(new Point(), panel).Y >= rankingsBottom);
            else Assert.Equal(2, Grid.GetColumn(journal));
            Assert.Equal(3, sideColumn.Children.Count);
            var picker = (ListBox)window.FindName("RecentWorkPicker");
            picker.SelectedIndex = 1;
            panel.UpdateLayout();
            Assert.Same(window.DashboardRecentWorks[1], picker.SelectedItem);
            Assert.Contains(Descendants(panel).OfType<ContentControl>(), control => ReferenceEquals(control.Content, picker.SelectedItem));
            SaveSnapshot(panel, $"dashboard-showcase-{width:0}.png");
        }
        ((Border)window.FindName("DashboardHero")).Visibility = Visibility.Collapsed;
        ((Grid)window.FindName("DashboardCollectionHeader")).Visibility = Visibility.Visible;
        var scroll = (ScrollViewer)window.FindName("DashboardScroll");
        var root = (Grid)window.Content;
        window.Content = null;
        root.DataContext = window;
        root.Resources = window.Resources;
        window.DashboardTopBooks.RemoveAt(2);
        window.DashboardTopAuthors.RemoveAt(2);
        window.DashboardTopScreens.Add(window.DashboardTopBooks[1]);
        window.DashboardTimelineDays.Add(window.DashboardTimelineDays[0]);
        foreach (var height in new[] { 800d, 768d, 640d, 800d })
        {
            scroll.Visibility = Visibility.Visible;
            root.Measure(new Size(1280, height));
            root.Arrange(new Rect(0, 0, 1280, height));
            root.UpdateLayout();
            Assert.True(height < 700 || scroll.ScrollableHeight == 0, $"height={height}, extent={scroll.ExtentHeight}, viewport={scroll.ViewportHeight}, desired={scroll.DesiredSize}");
        }
        SaveSnapshot(root, "dashboard-full.png");
        var dashboardAdd = (Button)window.FindName("DashboardAddWorkButton");
        var dashboardBounds = new Rect(dashboardAdd.TranslatePoint(new Point(), root), dashboardAdd.RenderSize);
        typeof(MainWindow).GetMethod("Library_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
        root.Measure(new Size(1280, 800));
        root.Arrange(new Rect(0, 0, 1280, 800));
        root.UpdateLayout();
        var libraryAdd = (Button)window.FindName("LibraryAddWorkButton");
        Assert.Equal(dashboardBounds, new Rect(libraryAdd.TranslatePoint(new Point(), root), libraryAdd.RenderSize));
    }

    private static void AssertLibraryLayout()
    {
        var window = new MainWindow();
        typeof(MainWindow).GetMethod("Library_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("LibraryPane")).Visibility);
        ((FrameworkElement)window.FindName("EmptyState")).Visibility = Visibility.Collapsed;
        var list = (ListBox)window.FindName("WorkList");
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
            window.VisibleWorks.Add(new MediaWork { Title = "走夜路请放声歌唱", Kind = "book", AggregateRank = 2.4,
                ExperienceCount = 1, LatestActivityOn = new DateOnly(2026, 9, 18) });
        Layout(768);
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
            window.VisibleWorks.Add(new MediaWork { Title = $"作品 {index}", Kind = "book" });
        Layout(800);
        Assert.True(scroll.ScrollableHeight > 0, $"items={list.Items.Count}, extent={scroll.ExtentHeight}, viewport={scroll.ViewportHeight}, width={scroll.ExtentWidth}");
        Assert.Equal(Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility);
        list.ScrollIntoView(window.VisibleWorks[^1]);
        root.UpdateLayout();
        Assert.True(scroll.VerticalOffset > 0);
        Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(19));
        typeof(MainWindow).GetMethod("Home_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
        Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("LibraryPane")).Visibility);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("NavigationRail")).Visibility);
        Assert.Equal(126, ((ColumnDefinition)window.FindName("LibraryColumn")).Width.Value);
        host.Close();
        window.Close();
    }

    private static void AssertDetailLayout()
    {
        var window = new MainWindow();
        ((ScrollViewer)window.FindName("DashboardScroll")).Visibility = Visibility.Collapsed;
        ((FrameworkElement)window.FindName("DetailEmpty")).Visibility = Visibility.Collapsed;
        ((FrameworkElement)window.FindName("HistoryEmpty")).Visibility = Visibility.Collapsed;
        var scroll = (ScrollViewer)window.FindName("DetailScroll");
        scroll.Visibility = Visibility.Visible;
        foreach (var (name, text) in new[]
        {
            ("DetailTitleText", "真事隐"), ("DetailSubtitleText", "康熙废储与正史虚构"),
            ("DetailAuthorText", "孙立天"), ("DetailMetaText", "书籍 · 已记录 1 次 · 最近 2026-09-13"),
            ("DetailRankText", "3.5 / 3.9"), ("DetailCountText", "阅读 1 次"),
            ("DetailRatingCountText", "来自 1 次评分"), ("HistoryCaptionText", "共 1 次")
        })
        {
            var label = (TextBlock)window.FindName(name);
            label.Text = text;
            label.Visibility = Visibility.Visible;
        }
        window.CompletedExperiences.Add(new ExperienceArchiveCard
        {
            ArchiveNumber = 1,
            Experience = new MediaExperience { WorkId = "detail-layout", CompletedOn = new DateOnly(2026, 9, 13),
                Allure = 3, Immersion = 4, Rationality = 5, Illumination = 4 }
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
        window.CompletedExperiences.Add(window.CompletedExperiences[0]);
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
        var timeline = Assert.IsType<ItemsControl>(mainWindow.FindName("DashboardTimelineList"));
        var presenter = new ContentPresenter
        {
            ContentTemplate = timeline.ItemTemplate,
            Resources = mainWindow.Resources,
            Width = 280,
            Content = new DashboardTimelineDay
            {
                Date = new DateOnly(2026, 8, 28),
                Items = [new DashboardTimelineItem
                {
                    Id = "completion", WorkId = "timeline-test", Title = "一本完成的书", Kind = "book",
                    Notes = "合上书之后，仍然记得这一段旅程。"
                }]
            }
        };
        presenter.Measure(new Size(presenter.Width, double.PositiveInfinity));
        presenter.Arrange(new Rect(presenter.DesiredSize));
        presenter.UpdateLayout();
        var buttons = Descendants(presenter).OfType<Button>().ToArray();
        Assert.Single(buttons);
        Assert.All(buttons, button =>
        {
            Assert.IsType<DashboardTimelineItem>(button.Tag);
            Assert.True(button.ActualWidth > 180, "Timeline actions should fill the date card.");
            Assert.True(button.Focusable);
        });
        SaveSnapshot(presenter, "timeline-day.png");
    }

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
        var history = Assert.IsType<ListBox>(mainWindow.FindName("HistoryList"));
        var presenter = new ContentPresenter
        {
            ContentTemplate = history.ItemTemplate,
            Resources = mainWindow.Resources,
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
