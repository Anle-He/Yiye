using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Yiye.Controls;
using Yiye.Models;
using Yiye.Presentation;
namespace Yiye.Views;

public partial class LibraryView : UserControl
{
    private const double WorkTileWidth = 118;
    private const double WorkRowHeight = 208;
    private const double WorkGroupOverhead = 64;
    private const double MonthTileWidth = 220;
    private const double MonthRowHeight = 232;
    private IReadOnlyList<MediaWork> _works = [];
    private CancellationTokenSource? _search;
    private bool _refreshing;
    private int _rememberedPageIndex;
    private int _monthOverviewPageIndex;
    private int _monthPageSize = 1;
    private readonly CoverMotion _coverMotion = new();
    private readonly ListCollectionView _pageWorksView;
    private readonly List<IReadOnlyList<WorkCard>> _workPages = [];

    public bool IsMonthView { get; private set; }
    public int PageIndex { get; private set; }
    public int PageCount { get; private set; } = 1;
    public string ReturnLabel => Selection.ExpandedMonthKey is { } key && LibraryMonths.FirstOrDefault(month => month.Key == key) is { } month
        ? $"返回 {month.Label}" : "返回作品库";
    public LibrarySelection Selection { get; } = new();
    public ObservableCollection<WorkCard> VisibleWorks { get; } = [];
    public ObservableCollection<LibraryMonth> LibraryMonths { get; } = [];
    public ObservableCollection<WorkCard> ExpandedMonthWorks { get; } = [];
    public ObservableCollection<WorkCard> PageWorks { get; } = [];
    public ObservableCollection<LibraryMonth> PageMonths { get; } = [];
    public event Action? AddRequested;
    public event Action<string>? WorkRequested;

    private bool IsMonthOverview => IsMonthView && Selection.ExpandedMonthKey is null;
    private IReadOnlyList<WorkCard> CurrentWorks => IsMonthView && Selection.ExpandedMonthKey is not null
        ? ExpandedMonthWorks : VisibleWorks;

    public LibraryView()
    {
        InitializeComponent();
        _pageWorksView = new ListCollectionView(PageWorks);
        var grouping = new PropertyGroupDescription(nameof(WorkCard.KindLabel));
        grouping.GroupNames.Add("书籍");
        grouping.GroupNames.Add("影视");
        _pageWorksView.GroupDescriptions.Add(grouping);
        WorkList.ItemsSource = _pageWorksView;
        DataContext = this;
        Unloaded += (_, _) => { CancelSearch(); _coverMotion.Cancel(); };
        SizeChanged += (_, _) => _coverMotion.Cancel();
    }

    public void SetWorks(IReadOnlyList<MediaWork> works)
    {
        _works = works;
        ApplyFilters();
    }

    public void FocusSearch() => SearchBox.Focus();

    public void SetMonthView(bool monthView)
    {
        _coverMotion.Cancel();
        IsMonthView = monthView;
        Selection.Expand(null);
        PageIndex = 0;
        _monthOverviewPageIndex = 0;
        RefreshMonths();
    }

    public void SetFilter(string kind, string query)
    {
        CancelSearch();
        Selection.SetFilter(kind, query);
        SearchBox.Text = query;
        CancelSearch();
        PageIndex = 0;
        _monthOverviewPageIndex = 0;
        ApplyFilters();
    }

    public void ApplyFilters()
    {
        _coverMotion.Cancel();
        Selection.SetFilter(Selection.Kind, SearchBox.Text);
        VisibleWorks.Clear();
        foreach (var work in Selection.Filter(_works)) VisibleWorks.Add(work);
        RefreshMonths();
        LibraryCountText.Text = $"{_works.Count} 部作品";
        EmptyTitle.Text = _works.Count == 0 ? "还没有作品" : "没有找到作品";
        EmptyDescription.Text = _works.Count == 0 ? "添加一本书或一部影视。" : "试试其他关键词，或切换到全部类别。";
        foreach (var button in new[] { AllFilterButton, BookFilterButton, ScreenFilterButton })
            System.Windows.Controls.Primitives.Selector.SetIsSelected(button, Equals(button.Tag, Selection.Kind));
    }

    public void RefreshMonths()
    {
        _refreshing = true;
        try
        {
            LibraryMonths.Clear();
            foreach (var month in LibraryMonth.Group(VisibleWorks)) LibraryMonths.Add(month);
            var expanded = IsMonthView ? LibraryMonths.FirstOrDefault(month => month.Key == Selection.ExpandedMonthKey) : null;
            ExpandedMonthWorks.Clear();
            if (expanded is not null) foreach (var work in expanded.Works) ExpandedMonthWorks.Add(work);
            else Selection.Expand(null);
            MonthBackButton.Visibility = expanded is null ? Visibility.Collapsed : Visibility.Visible;
            RegularWorksHeader.Visibility = expanded is null ? Visibility.Collapsed : Visibility.Visible;
            LibraryMonthHeading.Text = expanded is null ? string.Empty : $"{expanded.Label} · {expanded.CountLabel}";
            System.Windows.Controls.Primitives.Selector.SetIsSelected(CoverViewButton, !IsMonthView);
            System.Windows.Controls.Primitives.Selector.SetIsSelected(MonthViewButton, IsMonthView);
        }
        finally { _refreshing = false; }
        RebuildPages();
    }

    private void RebuildPages(bool keepFirstWork = false)
    {
        var firstWorkId = keepFirstWork ? PageWorks.FirstOrDefault()?.Id : null;
        var width = PageViewport.ActualWidth > 0 ? PageViewport.ActualWidth : 1100;
        var height = PageViewport.ActualHeight > 0 ? PageViewport.ActualHeight : 530;
        _workPages.Clear();
        if (IsMonthOverview)
        {
            var columns = Math.Max(1, (int)(width / MonthTileWidth));
            var rows = Math.Max(1, (int)(height / MonthRowHeight));
            _monthPageSize = columns * rows;
            PageCount = Math.Max(1, (int)Math.Ceiling((double)LibraryMonths.Count / _monthPageSize));
        }
        else
        {
            // These measurements match the compact item and group templates in LibraryView.xaml.
            var columns = Math.Max(1, (int)((width - 28) / WorkTileWidth));
            var page = new List<WorkCard>();
            var counts = new Dictionary<string, int>();
            foreach (var work in CurrentWorks.OrderBy(work => work.Kind == "book" ? 0 : 1))
            {
                counts[work.Kind] = counts.GetValueOrDefault(work.Kind) + 1;
                var neededHeight = counts.Sum(group => WorkGroupOverhead + Math.Ceiling((double)group.Value / columns) * WorkRowHeight);
                if (page.Count > 0 && neededHeight > height)
                {
                    _workPages.Add(page.ToArray());
                    page.Clear();
                    counts.Clear();
                    counts[work.Kind] = 1;
                }
                page.Add(work);
            }
            if (page.Count > 0) _workPages.Add(page.ToArray());
            PageCount = Math.Max(1, _workPages.Count);
            if (firstWorkId is not null)
            {
                var containingPage = _workPages.FindIndex(pageWorks => pageWorks.Any(work => work.Id == firstWorkId));
                if (containingPage >= 0) PageIndex = containingPage;
            }
        }
        PageIndex = Math.Clamp(PageIndex, 0, PageCount - 1);
        ShowCurrentPage();
    }

    public void SetPage(int pageIndex)
    {
        _coverMotion.Cancel();
        PageIndex = Math.Clamp(pageIndex, 0, PageCount - 1);
        ShowCurrentPage();
    }

    public void NextPage() => SetPage(PageIndex + 1);
    public void PreviousPage() => SetPage(PageIndex - 1);

    private void ShowCurrentPage()
    {
        _refreshing = true;
        try
        {
            PageWorks.Clear();
            if (!IsMonthOverview && _workPages.Count > 0)
                foreach (var work in _workPages[PageIndex]) PageWorks.Add(work);
            PageMonths.Clear();
            if (IsMonthOverview)
                foreach (var month in LibraryMonths.Skip(PageIndex * _monthPageSize).Take(_monthPageSize)) PageMonths.Add(month);
            WorkList.SelectedItem = null;
            MonthOverview.Visibility = IsMonthOverview && PageMonths.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            WorkList.Visibility = !IsMonthOverview && PageWorks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = VisibleWorks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LibraryPager.Visibility = VisibleWorks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            PreviousPageButton.IsEnabled = PageIndex > 0;
            NextPageButton.IsEnabled = PageIndex + 1 < PageCount;
            PageIndicatorText.Text = $"{PageIndex + 1} / {PageCount}";
            PageSummaryText.Text = IsMonthOverview
                ? $"本页 {PageMonths.Count} 组，共 {LibraryMonths.Count} 组"
                : $"本页 {PageWorks.Count} 部，共 {CurrentWorks.Count} 部";
        }
        finally { _refreshing = false; }
    }

    private void PageViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_refreshing && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            _coverMotion.Cancel();
            RebuildPages(keepFirstWork: true);
        }
    }

    public void OpenMonth(string key)
    {
        _coverMotion.Cancel();
        var first = LibraryMonths.FirstOrDefault(month => month.Key == key)?.Works.FirstOrDefault();
        var image = first is null ? null : CoverMotion.Find(MonthList, first.Id);
        var from = CoverMotion.Bounds(image, MonthTransitionLayer);
        _monthOverviewPageIndex = IsMonthOverview ? PageIndex : 0;
        IsMonthView = true;
        Selection.Expand(key);
        PageIndex = 0;
        RefreshMonths();
        WorkList.UpdateLayout();
        MonthBackButton.Focus();
        if (first is not null)
            _coverMotion.Play(MonthTransitionLayer, from, CoverMotion.Find(WorkList, first.Id), image?.Source);
    }

    public void RememberPosition(string workId)
    {
        _coverMotion.Cancel();
        _rememberedPageIndex = PageIndex;
    }

    public void RestorePosition(string workId)
    {
        RebuildPages();
        var containingPage = _workPages.FindIndex(page => page.Any(work => work.Id == workId));
        SetPage(containingPage >= 0 ? containingPage : _rememberedPageIndex);
        WorkList.UpdateLayout();
        if (FindWorkContainer(WorkList, workId) is { } container)
        {
            _refreshing = true;
            try { container.Focus(); WorkList.SelectedItem = null; }
            finally { _refreshing = false; }
        }
    }

    private static ListBoxItem? FindWorkContainer(DependencyObject root, string workId)
    {
        if (root is ListBoxItem { DataContext: WorkCard work } container && work.Id == workId) return container;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            if (FindWorkContainer(VisualTreeHelper.GetChild(root, index), workId) is { } found) return found;
        return null;
    }

    private void OpenLibraryMonth_Click(object sender, RoutedEventArgs e)
    { if (sender is Button { Tag: LibraryMonth month }) OpenMonth(month.Key); }

    private void CloseLibraryMonth_Click(object sender, RoutedEventArgs e)
    {
        _coverMotion.Cancel();
        var first = PageWorks.FirstOrDefault();
        var image = first is null ? null : CoverMotion.Find(WorkList, first.Id);
        var from = CoverMotion.Bounds(image, MonthTransitionLayer);
        var key = Selection.ExpandedMonthKey;
        Selection.Expand(null);
        PageIndex = _monthOverviewPageIndex;
        RefreshMonths();
        MonthOverview.UpdateLayout();
        var monthIndex = LibraryMonths.ToList().FindIndex(month => month.Key == key);
        if (monthIndex >= 0) SetPage(monthIndex / _monthPageSize);
        MonthOverview.UpdateLayout();
        var index = PageMonths.ToList().FindIndex(month => month.Key == key);
        if (index >= 0 && MonthList.ItemContainerGenerator.ContainerFromIndex(index) is ContentPresenter presenter)
        {
            presenter.ApplyTemplate();
            (MonthList.ItemTemplate.FindName("MonthDeckButton", presenter) as Button)?.Focus();
        }
        if (first is not null)
            _coverMotion.Play(MonthTransitionLayer, from, CoverMotion.Find(MonthList, first.Id), image?.Source);
    }

    private void PreviousPage_Click(object sender, RoutedEventArgs e) => PreviousPage();
    private void NextPage_Click(object sender, RoutedEventArgs e) => NextPage();
    private void AddWork_Click(object sender, RoutedEventArgs e) => AddRequested?.Invoke();
    private void WorkList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_refreshing && WorkList.SelectedItem is WorkCard work)
        {
            WorkRequested?.Invoke(work.Id);
            _refreshing = true;
            try { WorkList.SelectedItem = null; }
            finally { _refreshing = false; }
        }
    }
    private void Filter_Click(object sender, RoutedEventArgs e)
    { if (sender is Button { Tag: string kind }) SetFilter(kind, SearchBox.Text); }
    private void ViewMode_Click(object sender, RoutedEventArgs e)
    { if (sender is Button { Tag: string mode }) SetMonthView(mode == "months"); }
    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CancelSearch();
        var cancellation = _search = new CancellationTokenSource();
        try
        {
            await Task.Delay(200, cancellation.Token);
            if (_search == cancellation) { PageIndex = 0; ApplyFilters(); }
        }
        catch (OperationCanceledException) { }
    }
    private void CancelSearch() { _search?.Cancel(); _search?.Dispose(); _search = null; }
}
