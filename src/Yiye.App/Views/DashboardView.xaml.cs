using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Yiye.Operations;
using Yiye.Models;
using Yiye.Presentation;
namespace Yiye.Views;

public partial class DashboardView : UserControl
{
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
        foreach (var day in snapshot.Timeline.GroupBy(item => item.LoggedOn))
            DashboardTimelineDays.Add(new DashboardTimelineDay { Date = day.Key, Items = day.Select(item => new TimelineCard(item)).ToArray() });
        DashboardHero.Visibility = works.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DashboardCollectionHeader.Visibility = works.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DashboardWorkCountText.Text = works.Count.ToString();
        DashboardActiveCountText.Text = works.Sum(work => work.RatedExperienceCount).ToString();
        DashboardExperienceCountText.Text = works.Sum(work => work.ExperienceCount).ToString();
        DashboardEmptyState.Visibility = works.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DashboardTimelineList.Visibility = snapshot.Timeline.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        DashboardTimelineEmptyState.Visibility = works.Count > 0 && snapshot.Timeline.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        static IEnumerable<DashboardShowcaseItem> Ranked(IEnumerable<DashboardShowcaseItem> items) => items.Where(item => item.AggregateRank is not null)
            .OrderByDescending(item => item.AggregateRank).ThenByDescending(item => item.RatingCount).ThenByDescending(item => item.LatestCompletedOn).Take(3);
        DashboardTopBooks.Clear();
        foreach (var item in Ranked(snapshot.Showcase.CompletedWorks.Where(item => item.Kind == "book"))) DashboardTopBooks.Add(new(item));
        DashboardTopScreens.Clear();
        foreach (var item in Ranked(snapshot.Showcase.CompletedWorks.Where(item => item.Kind == "screen"))) DashboardTopScreens.Add(new(item));
        DashboardRecentWorks.Clear();
        foreach (var item in snapshot.Showcase.CompletedWorks.OrderByDescending(item => item.LatestCompletedOn).Take(3)) DashboardRecentWorks.Add(new(item));
        DashboardTopAuthors.Clear();
        foreach (var item in snapshot.Showcase.TopAuthors) DashboardTopAuthors.Add(new(item));
        RecentWorkPicker.SelectedIndex = DashboardRecentWorks.Count > 0 ? 0 : -1;
        DashboardShowcasePanel.Visibility = works.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
    private void AddWork_Click(object sender, RoutedEventArgs e) => AddRequested?.Invoke();
    private void DashboardWork_Open(object sender, RoutedEventArgs e)
    {
        var id = (sender as FrameworkElement)?.Tag switch { TimelineCard item => item.WorkId, ShowcaseCard item => item.WorkId, _ => null };
        if (id is not null) WorkRequested?.Invoke(id);
    }
    private void DashboardColumns_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= 950;
        Grid.SetColumnSpan(DashboardMainColumn, wide ? 1 : 3);
        Grid.SetColumn(DashboardJournalSection, wide ? 2 : 0);
        Grid.SetRow(DashboardJournalSection, wide ? 0 : 2);
        Grid.SetColumnSpan(DashboardJournalSection, wide ? 1 : 3);
        DashboardJournalSection.Margin = wide ? new Thickness(0, 4, 0, 0) : new Thickness(0, 14, 0, 0);
        var stacked = e.NewSize.Width < 700;
        Border[] rankings = [DashboardBookRanking, DashboardScreenRanking, DashboardAuthorRanking];
        for (var index = 0; index < rankings.Length; index++)
        {
            Grid.SetRow(rankings[index], stacked ? index : 0);
            Grid.SetColumn(rankings[index], stacked ? 0 : index);
            Grid.SetColumnSpan(rankings[index], stacked ? 3 : 1);
            rankings[index].Margin = index == 0 ? new Thickness(0)
                : stacked ? new Thickness(0, 16, 0, 0) : new Thickness(24, 0, 0, 0);
            rankings[index].BorderBrush = (System.Windows.Media.Brush)FindResource("DividerBrush");
            rankings[index].BorderThickness = !stacked && index > 0 ? new Thickness(1, 0, 0, 0) : new Thickness(0);
        }
    }

}
