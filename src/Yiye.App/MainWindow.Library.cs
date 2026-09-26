using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Yiye.Models;

namespace Yiye;

public partial class MainWindow
{
    private string? _expandedMonthKey;
    private (string Kind, string Query)? _libraryMonthFilter;
    public ObservableCollection<LibraryMonth> LibraryMonths { get; } = [];
    public ObservableCollection<MediaWork> ExpandedMonthWorks { get; } = [];

    private void RefreshLibraryMonths(string? selectedWorkId = null)
    {
        LibraryMonths.Clear();
        foreach (var month in LibraryMonth.Group(VisibleWorks))
            LibraryMonths.Add(month);

        if (selectedWorkId is not null)
            _expandedMonthKey = LibraryMonths.FirstOrDefault(month => month.Works.Any(work => work.Id == selectedWorkId))?.Key;

        var expanded = LibraryMonths.FirstOrDefault(month => month.Key == _expandedMonthKey);
        ExpandedMonthWorks.Clear();
        if (expanded is not null)
            foreach (var work in expanded.Works)
                ExpandedMonthWorks.Add(work);
        else
            _expandedMonthKey = null;

        MonthOverview.Visibility = VisibleWorks.Count > 0 && expanded is null ? Visibility.Visible : Visibility.Collapsed;
        WorkList.Visibility = expanded is null ? Visibility.Collapsed : Visibility.Visible;
        MonthBackButton.Visibility = expanded is null ? Visibility.Collapsed : Visibility.Visible;
        LibraryMonthHeading.Text = expanded is null
            ? "按最近记录月份 · 点击卡叠展开"
            : $"{expanded.Label} · {expanded.CountLabel}";
    }

    private void OpenLibraryMonth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LibraryMonth month }) return;
        _expandedMonthKey = month.Key;
        RefreshLibraryMonths();
        WorkList.SelectedItem = null;
        WorkList.UpdateLayout();
        if (ExpandedMonthWorks.Count > 0)
            WorkList.ScrollIntoView(ExpandedMonthWorks[0]);
        MonthBackButton.Focus();
    }

    private void CloseLibraryMonth_Click(object sender, RoutedEventArgs e)
    {
        var closedKey = _expandedMonthKey;
        _expandedMonthKey = null;
        RefreshLibraryMonths();
        MonthOverview.UpdateLayout();
        for (var index = 0; index < LibraryMonths.Count; index++)
        {
            if (LibraryMonths[index].Key != closedKey) continue;
            if (MonthList.ItemContainerGenerator.ContainerFromIndex(index) is ContentPresenter presenter)
            {
                presenter.ApplyTemplate();
                var button = MonthList.ItemTemplate.FindName("MonthDeckButton", presenter) as Button;
                button?.Focus();
            }
            break;
        }
    }
}
