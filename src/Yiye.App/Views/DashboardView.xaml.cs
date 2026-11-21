using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yiye.Controls;
using Yiye.Operations;
using Yiye.Models;
using Yiye.Presentation;
namespace Yiye.Views;

public partial class DashboardView : UserControl
{
    private double _returnOffset;
    private double _shelfWidth;
    private Button? _returnAction;
    public ObservableCollection<ShowcaseCard> DashboardTopBooks { get; } = [];
    public ObservableCollection<ShowcaseCard> DashboardTopScreens { get; } = [];
    public ObservableCollection<ShowcaseCard> DashboardRecentWorks { get; } = [];
    public ObservableCollection<AuthorCard> DashboardTopAuthors { get; } = [];
    public event Action? AddRequested;
    public event Action<string>? WorkRequested;
    public DashboardView() { InitializeComponent(); DataContext = this; }
    public void Render(IReadOnlyList<MediaWork> works, DashboardSnapshot snapshot)
    {
        DashboardHero.Visibility = works.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DashboardCollectionHeader.Visibility = works.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var overview = DashboardOverview.FromCompletedWorks(snapshot.Showcase.CompletedWorks);
        DashboardBookCountText.Text = overview.BookCount.ToString();
        DashboardScreenCountText.Text = overview.ScreenCount.ToString();
        DashboardBookShareText.Text = $"书籍 {overview.BookShare:P0}";
        DashboardScreenShareText.Text = $"影视 {overview.ScreenShare:P0}";
        BookRatioColumn.Width = new GridLength(overview.BookCount, GridUnitType.Star);
        ScreenRatioColumn.Width = new GridLength(overview.ScreenCount, GridUnitType.Star);
        DashboardCompletionRatio.Visibility = overview.CompletedWorkCount == 0 ? Visibility.Collapsed : Visibility.Visible;
        DashboardBookRatio.CornerRadius = overview.ScreenCount == 0 ? new CornerRadius(4) : new CornerRadius(4, 0, 0, 4);
        DashboardScreenRatio.CornerRadius = overview.BookCount == 0 ? new CornerRadius(4) : new CornerRadius(0, 4, 4, 0);
        DashboardCollectionHeader.ToolTip = $"收藏 {works.Count} 部作品 · {works.Sum(work => work.RatedExperienceCount)} 次评分 · {works.Sum(work => work.ExperienceCount)} 次完成";
        DashboardStats.Visibility = works.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        static IEnumerable<DashboardShowcaseItem> Ranked(IEnumerable<DashboardShowcaseItem> items) => items.Where(item => item.AggregateRank is not null)
            .OrderByDescending(item => item.AggregateRank).ThenByDescending(item => item.RatingCount).ThenByDescending(item => item.LatestCompletedOn).Take(3);
        DashboardTopBooks.Clear();
        foreach (var item in Ranked(snapshot.Showcase.CompletedWorks.Where(item => item.Kind == "book"))) DashboardTopBooks.Add(new(item, DashboardTopBooks.Count + 1));
        DashboardTopScreens.Clear();
        foreach (var item in Ranked(snapshot.Showcase.CompletedWorks.Where(item => item.Kind == "screen"))) DashboardTopScreens.Add(new(item, DashboardTopScreens.Count + 1));
        DashboardRecentWorks.Clear();
        foreach (var item in snapshot.Showcase.CompletedWorks.Where(item => item.CompletionCount > 0)
            .OrderByDescending(item => item.LatestCompletedOn).DistinctBy(item => item.WorkId).Take(6)) DashboardRecentWorks.Add(new(item));
        DashboardRecentEmptyState.Visibility = DashboardRecentWorks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DashboardTopAuthors.Clear();
        foreach (var item in AuthorCard.CreateCloud(snapshot.Showcase.TopAuthors)) DashboardTopAuthors.Add(item);
        if (DashboardTopAuthors.Count == 0) AuthorCloudCount.Text = string.Empty;
        DashboardShowcasePanel.Visibility = works.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
    private void AddWork_Click(object sender, RoutedEventArgs e) => AddRequested?.Invoke();
    private void AuthorCloud_VisibleCountChanged(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is WordCloudPanel cloud)
        {
            var label = cloud.VisibleCount > 0 ? $"前 {cloud.VisibleCount} 位" : string.Empty;
            if (AuthorCloudCount.Text != label) AuthorCloudCount.Text = label;
        }
    }
    private void AuthorWord_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { ToolTip: ToolTip tip } word)
        {
            tip.PlacementTarget = word;
            tip.IsOpen = true;
        }
    }
    private void AuthorWord_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { ToolTip: ToolTip tip }) tip.IsOpen = false;
    }
    private void AuthorWord_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false && sender is FrameworkElement { ToolTip: ToolTip tip }) tip.IsOpen = false;
    }
    private void AuthorWord_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && sender is FrameworkElement { ToolTip: ToolTip tip })
        {
            tip.IsOpen = false;
            e.Handled = true;
        }
    }
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
            ShowcaseCard card => card.WorkId == workId,
            _ => false
        };
    private void DashboardWork_Open(object sender, RoutedEventArgs e)
    {
        _returnAction = sender as Button;
        var id = ((sender as FrameworkElement)?.Tag as ShowcaseCard)?.WorkId;
        if (id is not null) WorkRequested?.Invoke(id);
    }
    private void Dashboard_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDensity();
    }
    private void DashboardRecentSection_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _shelfWidth = e.NewSize.Width;
        UpdateDensity();
    }
    private void UpdateDensity()
    {
        var availableWidth = ActualWidth > 0 ? ActualWidth : _shelfWidth + 56;
        var compact = ActualHeight is > 0 and < 600 || availableWidth is > 0 and < 996;
        Tag = compact ? "Compact" : "Regular";
        DashboardContent.Margin = compact ? new Thickness(24, 4, 24, 12) : new Thickness(28, 12, 28, 20);
        DashboardCollectionHeader.Margin = new Thickness(0, 0, 0, compact ? 16 : 24);
        DashboardBookCountText.FontSize = DashboardScreenCountText.FontSize = compact ? 40 : 48;
        DashboardBookCountText.LineHeight = DashboardScreenCountText.LineHeight = compact ? 46 : 56;
        RecentHeading.Margin = new Thickness(0, 0, 0, compact ? 8 : 10);
        DashboardColumns.Margin = new Thickness(0, compact ? 12 : 24, 0, 0);
        BookHeadingPanel.Margin = ScreenHeadingPanel.Margin = AuthorHeadingPanel.Margin = new Thickness(0, 0, 0, compact ? 6 : 10);
    }
}
