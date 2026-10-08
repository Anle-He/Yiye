using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Yiye.Models;
using Yiye.Presentation;
namespace Yiye.Views;

public partial class LibraryView : UserControl
{
    private IReadOnlyList<MediaWork> _works = [];
    private CancellationTokenSource? _search;
    private bool _refreshing;
    public LibrarySelection Selection { get; } = new();
    public ObservableCollection<WorkCard> VisibleWorks { get; } = [];
    public ObservableCollection<LibraryMonth> LibraryMonths { get; } = [];
    public ObservableCollection<WorkCard> ExpandedMonthWorks { get; } = [];
    public event Action? AddRequested;
    public event Action<string>? WorkRequested;
    public LibraryView()
    {
        InitializeComponent();
        DataContext = this;
        Unloaded += (_, _) => CancelSearch();
    }
    public void SetWorks(IReadOnlyList<MediaWork> works)
    {
        _works = works;
        ApplyFilters();
    }
    public void FocusSearch() => SearchBox.Focus();
    public void SetFilter(string kind, string query)
    {
        CancelSearch();
        Selection.SetFilter(kind, query);
        SearchBox.Text = query;
        CancelSearch();
        ApplyFilters();
    }
    public void ApplyFilters()
    {
        Selection.SetFilter(Selection.Kind, SearchBox.Text);
        VisibleWorks.Clear();
        foreach (var work in Selection.Filter(_works)) VisibleWorks.Add(work);
        RefreshMonths();
        LibraryCountText.Text = $"{_works.Count} 部作品";
        EmptyState.Visibility = VisibleWorks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RegularWorksHeader.Visibility = VisibleWorks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyTitle.Text = _works.Count == 0 ? "这里还没有作品" : "没有找到相符的作品";
        EmptyDescription.Text = _works.Count == 0 ? "先加入一本书，或一部想留下来的影视。" : "换一个标题关键词或类别试试。";
        foreach (var button in new[] { AllFilterButton, BookFilterButton, ScreenFilterButton })
            button.Appearance = Equals(button.Tag, Selection.Kind) ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
    }
    public void RefreshMonths()
    {
        _refreshing = true;
        try
        {
            LibraryMonths.Clear();
            foreach (var month in LibraryMonth.Group(VisibleWorks)) LibraryMonths.Add(month);
            var expanded = LibraryMonths.FirstOrDefault(month => month.Key == Selection.ExpandedMonthKey);
            ExpandedMonthWorks.Clear();
            if (expanded is not null) foreach (var work in expanded.Works) ExpandedMonthWorks.Add(work);
            else Selection.Expand(null);
            MonthOverview.Visibility = VisibleWorks.Count > 0 && expanded is null ? Visibility.Visible : Visibility.Collapsed;
            WorkList.Visibility = expanded is null ? Visibility.Collapsed : Visibility.Visible;
            MonthBackButton.Visibility = expanded is null ? Visibility.Collapsed : Visibility.Visible;
            LibraryMonthHeading.Text = expanded is null ? "按最近记录月份 · 点击卡叠展开" : $"{expanded.Label} · {expanded.CountLabel}";
            WorkList.SelectedItem = null;
        }
        finally { _refreshing = false; }
    }
    public void OpenMonth(string key)
    {
        Selection.Expand(key);
        RefreshMonths();
        WorkList.UpdateLayout();
        if (ExpandedMonthWorks.Count > 0) WorkList.ScrollIntoView(ExpandedMonthWorks[0]);
        MonthBackButton.Focus();
    }
    private void OpenLibraryMonth_Click(object sender, RoutedEventArgs e)
    { if (sender is Button { Tag: LibraryMonth month }) OpenMonth(month.Key); }
    private void CloseLibraryMonth_Click(object sender, RoutedEventArgs e)
    {
        var key = Selection.ExpandedMonthKey;
        Selection.Expand(null);
        RefreshMonths();
        MonthOverview.UpdateLayout();
        var index = LibraryMonths.ToList().FindIndex(month => month.Key == key);
        if (index >= 0 && MonthList.ItemContainerGenerator.ContainerFromIndex(index) is ContentPresenter presenter)
        {
            presenter.ApplyTemplate();
            (MonthList.ItemTemplate.FindName("MonthDeckButton", presenter) as Button)?.Focus();
        }
    }
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
    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CancelSearch();
        var cancellation = _search = new CancellationTokenSource();
        try
        {
            await Task.Delay(200, cancellation.Token);
            if (_search == cancellation) ApplyFilters();
        }
        catch (OperationCanceledException) { }
    }
    private void CancelSearch() { _search?.Cancel(); _search?.Dispose(); _search = null; }
}
