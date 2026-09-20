using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Yiye.Models;

namespace Yiye;

public partial class MainWindow
{
    private async Task ReloadDashboardAsync()
    {
        if (_repository is null)
        {
            return;
        }

        var timelineTask = _repository.GetRecentTimelineAsync();
        var showcaseTask = _repository.GetDashboardShowcaseAsync();
        _dashboardTimelineItems = await timelineTask;
        _dashboardShowcase = await showcaseTask;
        RefreshDashboard();
    }

    private void RefreshDashboard()
    {
        DashboardTimelineDays.Clear();
        foreach (var day in _dashboardTimelineItems.GroupBy(item => item.LoggedOn))
        {
            DashboardTimelineDays.Add(new DashboardTimelineDay { Date = day.Key, Items = day.ToList() });
        }

        DashboardHero.Visibility = _allWorks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DashboardCollectionHeader.Visibility = _allWorks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DashboardWorkCountText.Text = _allWorks.Count.ToString();
        DashboardActiveCountText.Text = _allWorks.Sum(work => work.RatedExperienceCount).ToString();
        DashboardExperienceCountText.Text = _allWorks.Sum(work => work.ExperienceCount).ToString();
        DashboardEmptyState.Visibility = _allWorks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DashboardTimelineList.Visibility = _dashboardTimelineItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        DashboardTimelineEmptyState.Visibility = _allWorks.Count > 0 && _dashboardTimelineItems.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        RefreshDashboardShowcase();
    }

    private void RefreshDashboardShowcase()
    {
        static IEnumerable<DashboardShowcaseItem> Ranked(IEnumerable<DashboardShowcaseItem> works) =>
            works.Where(work => work.AggregateRank is not null)
                .OrderByDescending(work => work.AggregateRank)
                .ThenByDescending(work => work.RatingCount)
                .ThenByDescending(work => work.LatestCompletedOn)
                .Take(3);

        DashboardTopBooks.Clear();
        foreach (var work in Ranked(_dashboardShowcase.CompletedWorks.Where(work => work.Kind == "book")))
        {
            DashboardTopBooks.Add(work);
        }
        DashboardTopScreens.Clear();
        foreach (var work in Ranked(_dashboardShowcase.CompletedWorks.Where(work => work.Kind == "screen")))
        {
            DashboardTopScreens.Add(work);
        }
        DashboardRecentWorks.Clear();
        foreach (var work in _dashboardShowcase.CompletedWorks.OrderByDescending(work => work.LatestCompletedOn).Take(3))
        {
            DashboardRecentWorks.Add(work);
        }
        DashboardTopAuthors.Clear();
        RecentWorkPicker.SelectedIndex = DashboardRecentWorks.Count > 0 ? 0 : -1;
        foreach (var author in _dashboardShowcase.TopAuthors)
        {
            DashboardTopAuthors.Add(author);
        }

        DashboardShowcasePanel.Visibility = _allWorks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
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

    private void SetLibraryPaneVisible(bool visible)
    {
        var changed = _showingLibrary != visible;
        _showingLibrary = visible;
        LibraryPane.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ContentPane.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        LibraryButton.Appearance = visible ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Transparent;
        if (changed && IsLoaded && SystemParameters.ClientAreaAnimation)
        {
            var page = visible ? (FrameworkElement)LibraryPane : ContentPane;
            page.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            });
        }
    }

    private void Library_Click(object sender, RoutedEventArgs e)
    {
        _selectionLoadVersion++;
        _showingDashboard = false;
        _selectedWorkId = null;
        _selectedWork = null;
        SetLibraryPaneVisible(true);
        _isApplyingFilters = true;
        try { WorkList.SelectedItem = null; }
        finally { _isApplyingFilters = false; }
        HomeButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent;
        if (IsLoaded) SearchBox.Focus();
    }

    private async void DashboardWork_Open(object sender, RoutedEventArgs e)
    {
        var workId = (sender as FrameworkElement)?.Tag switch
        {
            DashboardTimelineItem item => item.WorkId,
            DashboardShowcaseItem item => item.WorkId,
            _ => null
        };
        if (workId is null || _repository is null)
        {
            return;
        }

        _showingDashboard = false;
        _showingLibrary = false;
        _selectedWorkId = workId;
        _kindFilter = "all";
        UpdateFilterButtons();
        SearchBox.Clear();
        CancelPendingSearch();
        await ExecuteRepositoryActionAsync(() => ApplyFiltersAsync(reloadSelected: true), "无法打开作品");
    }
}
