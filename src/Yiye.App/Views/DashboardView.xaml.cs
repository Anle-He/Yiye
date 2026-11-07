using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Yiye.Operations;
using Yiye.Models;
using Yiye.Presentation;
namespace Yiye.Views;

public partial class DashboardView : UserControl
{
    private double _returnOffset;
    private Button? _returnAction;
    public ObservableCollection<DashboardTimelineDay> DashboardTimelineDays { get; } = [];
    public ObservableCollection<ShowcaseCard> DashboardTopBooks { get; } = [];
    public ObservableCollection<ShowcaseCard> DashboardTopScreens { get; } = [];
    public ObservableCollection<ShowcaseCard> DashboardRecentWorks { get; } = [];
    public ObservableCollection<AuthorCard> DashboardTopAuthors { get; } = [];
    public event Action? AddRequested;
    public event Action<string>? WorkRequested;
    public DashboardView() { InitializeComponent(); DataContext = this; }
    public void Render(IReadOnlyList<MediaWork> works, DashboardSnapshot snapshot)
    {
        DashboardTimelineDays.Clear();
        foreach (var day in snapshot.Timeline.Take(3).GroupBy(item => item.LoggedOn))
            DashboardTimelineDays.Add(new DashboardTimelineDay { Date = day.Key, Items = day.Select(item => new TimelineCard(item)).ToArray() });
        DashboardHero.Visibility = works.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DashboardCollectionHeader.Visibility = works.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DashboardWorkCountText.Text = works.Count.ToString();
        DashboardActiveCountText.Text = works.Sum(work => work.RatedExperienceCount).ToString();
        DashboardExperienceCountText.Text = works.Sum(work => work.ExperienceCount).ToString();
        DashboardStats.Visibility = works.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        DashboardTimelineList.Visibility = snapshot.Timeline.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        DashboardTimelineEmptyState.Visibility = works.Count > 0 && snapshot.Timeline.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        static IEnumerable<DashboardShowcaseItem> Ranked(IEnumerable<DashboardShowcaseItem> items) => items.Where(item => item.AggregateRank is not null)
            .OrderByDescending(item => item.AggregateRank).ThenByDescending(item => item.RatingCount).ThenByDescending(item => item.LatestCompletedOn).Take(3);
        DashboardTopBooks.Clear();
        foreach (var item in Ranked(snapshot.Showcase.CompletedWorks.Where(item => item.Kind == "book"))) DashboardTopBooks.Add(new(item));
        DashboardTopScreens.Clear();
        foreach (var item in Ranked(snapshot.Showcase.CompletedWorks.Where(item => item.Kind == "screen"))) DashboardTopScreens.Add(new(item));
        DashboardRecentWorks.Clear();
        foreach (var item in snapshot.Showcase.CompletedWorks.OrderByDescending(item => item.LatestCompletedOn).Take(6)) DashboardRecentWorks.Add(new(item));
        DashboardTopAuthors.Clear();
        foreach (var item in snapshot.Showcase.TopAuthors) DashboardTopAuthors.Add(new(item));
        DashboardShowcasePanel.Visibility = works.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
    private void AddWork_Click(object sender, RoutedEventArgs e) => AddRequested?.Invoke();
    public void RememberPosition() => _returnOffset = DashboardScroll.VerticalOffset;
    public void RestorePosition(string workId)
    {
        DashboardScroll.ScrollToVerticalOffset(_returnOffset);
        if (_returnAction is { IsVisible: true } && MatchesWork(_returnAction.Tag, workId)) _returnAction.Focus();
        else FindReturnAction(this, workId)?.Focus();
    }
    private static Button? FindReturnAction(DependencyObject root, string workId)
    {
        if (root is Button button && MatchesWork(button.Tag, workId)) return button;
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
            if (FindReturnAction(System.Windows.Media.VisualTreeHelper.GetChild(root, index), workId) is { } found) return found;
        return null;
    }
    private static bool MatchesWork(object? value, string workId) => value switch
        {
            TimelineCard card => card.WorkId == workId,
            ShowcaseCard card => card.WorkId == workId,
            _ => false
        };
    private void DashboardWork_Open(object sender, RoutedEventArgs e)
    {
        _returnAction = sender as Button;
        var id = (sender as FrameworkElement)?.Tag switch { TimelineCard item => item.WorkId, ShowcaseCard item => item.WorkId, _ => null };
        if (id is not null) WorkRequested?.Invoke(id);
    }
    private void Dashboard_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Height < 600;
        Tag = compact ? "Compact" : "Regular";
        DashboardContent.Margin = compact ? new Thickness(24, 6, 24, 8) : new Thickness(24, 8, 24, 16);
        DashboardCollectionHeader.Margin = new Thickness(0, 0, 0, compact ? 8 : 12);
        DashboardRecentSection.Padding = compact ? new Thickness(12, 10, 2, 2) : new Thickness(16, 14, 6, 8);
        DashboardSideColumn.Padding = new Thickness(compact ? 12 : 16);
        DashboardJournalSection.Padding = compact ? new Thickness(12, 10, 12, 10) : new Thickness(16);
        DashboardColumns.Margin = new Thickness(0, compact ? 8 : 12, 0, 0);
        BookHeadingPanel.Margin = ScreenHeadingPanel.Margin = new Thickness(0, 0, 0, compact ? 4 : 8);
        DashboardScreenRanking.Margin = new Thickness(0, compact ? 8 : 12, 0, 0);
        DashboardAuthorRanking.Margin = new Thickness(0, compact ? 6 : 12, 0, 0);
        DashboardAuthorRanking.Padding = new Thickness(0, compact ? 6 : 10, 0, 0);
    }
    private void DashboardColumns_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= 720;
        DashboardColumns.ColumnDefinitions[2].Width = new GridLength(e.NewSize.Width < 900 ? 250 : 280);
        Grid.SetColumnSpan(DashboardSideColumn, wide ? 1 : 3);
        Grid.SetColumn(DashboardJournalSection, wide ? 2 : 0);
        Grid.SetRow(DashboardJournalSection, wide ? 0 : 1);
        Grid.SetColumnSpan(DashboardJournalSection, wide ? 1 : 3);
        DashboardJournalSection.Margin = wide ? new Thickness(0) : new Thickness(0, 28, 0, 0);
    }
}
