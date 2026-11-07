using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Yiye.Operations;
using Yiye.Models;
using Yiye.Presentation;
using System.Windows.Media;
namespace Yiye.Views;

public partial class WorkDetailView : UserControl
{
    public MediaWork? Work { get; private set; }
    public ObservableCollection<ExperienceArchiveCard> CompletedExperiences { get; } = [];
    public ObservableCollection<ExperienceCard> UnfinishedExperiences { get; } = [];
    public event Action? CompletionRequested;
    public event Action<MediaExperience>? ExperienceRequested;
    public event Action? EditRequested;
    public event Action? DeleteRequested;
    public event Action? CoversRequested;
    public event Action? BackRequested;
    public FrameworkElement? CoverAnchor => DetailCovers.PrimaryCover;
    public ImageSource? CoverImageSource => DetailCovers.PrimaryImageSource;
    public bool IsCompact { get; private set; }
    public double ScrollOffset => DetailScroll.VerticalOffset;
    public void RestoreScroll(double offset) => DetailScroll.ScrollToVerticalOffset(offset);
    public void SetReturnPath(string label) => BackButton.Content = label;
    public WorkDetailView() { InitializeComponent(); DataContext = this; }
    public void Clear()
    {
        Work = null;
        CompletedExperiences.Clear();
        UnfinishedExperiences.Clear();
        DetailScroll.Visibility = Visibility.Collapsed;
        DetailEmpty.Visibility = Visibility.Visible;
    }
    public void Render(WorkDetail detail)
    {
        Work = detail.Work;
        DetailPathText.Text = detail.Work.Title;
        var work = new WorkCard(detail.Work);
        var allExperiences = detail.Experiences;
        DetailCovers.Render(detail.Work, detail.Covers);
        DetailKickerText.Text = work.Kind == "book" ? "书籍" : "影视";
        DetailTitleText.Text = work.Title;
        DetailSubtitleText.Text = work.Subtitle ?? string.Empty;
        DetailSubtitleText.Visibility = string.IsNullOrWhiteSpace(work.Subtitle)
            ? Visibility.Collapsed
            : Visibility.Visible;
        DetailAuthorText.Text = work.Author ?? string.Empty;
        DetailAuthorText.Visibility = work.Kind == "book" && !string.IsNullOrWhiteSpace(work.Author)
            ? Visibility.Visible
            : Visibility.Collapsed;
        DetailMetaText.Text = work.ExperienceCount == 0
            ? "尚无完成记录"
            : work.LatestActivityLabel;
        DetailRankValueRun.Text = work.HasAggregateRank ? work.AggregateRankValueLabel : "暂无评分";
        DetailRankMaximumRun.Text = work.HasAggregateRank ? $" {work.AggregateRankMaximumLabel}" : string.Empty;
        DetailRankText.FontSize = work.HasAggregateRank ? 32 : 16;
        DetailRankText.Foreground = (Brush)FindResource(work.HasAggregateRank ? "ScoreBrush" : "MutedStrongBrush");
        SummarySurface.Background = (Brush)FindResource(work.HasAggregateRank ? "ScoreSoftBrush" : "SurfaceSecondaryBrush");
        SummarySurface.BorderBrush = (Brush)FindResource(work.HasAggregateRank ? "DetailScoreBorderBrush" : "DividerBrush");
        DetailCountText.Text = work.ExperienceCountLabel;
        DetailRatingCountText.Text = work.RatingCountLabel;
        PrimaryExperienceButton.Content = work.Kind == "book" ? "记一次阅读" : "记一次观看";
        HistoryTitle.Text = work.Kind == "book" ? "阅读记录" : "观看记录";

        UnfinishedExperiences.Clear();
        foreach (var experience in allExperiences.Where(experience => experience.CompletedOn is null))
            UnfinishedExperiences.Add(new ExperienceCard(experience));
        UnfinishedHistory.Visibility = UnfinishedExperiences.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        CompletedExperiences.Clear();
        var completed = allExperiences.Where(experience => experience.CompletedOn is not null).ToList();
        for (var index = 0; index < completed.Count; index++)
        {
            CompletedExperiences.Add(new ExperienceArchiveCard
            {
                Experience = completed[index],
                ArchiveNumber = completed.Count - index
            });
        }
        HistoryCaptionText.Text = CompletedExperiences.Count == 0 ? string.Empty : $"共 {CompletedExperiences.Count} 次";
        HistoryEmpty.Visibility = CompletedExperiences.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryList.Visibility = CompletedExperiences.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        DetailEmpty.Visibility = Visibility.Collapsed;
        DetailScroll.Visibility = Visibility.Visible;
    }
    private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();
    private void Detail_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        IsCompact = e.NewSize.Width < 900;
        var compact = IsCompact;
        CoverColumn.Width = new GridLength(compact ? 260 : 300);
        CoverGap.Width = new GridLength(24);
        CoverPlate.Padding = new Thickness(compact ? 16 : 20);
        DetailRecordsPanel.Padding = new Thickness(compact ? 16 : 20);
        DetailCoverButton.Width = compact ? 108 : 160;
        DetailCoverButton.Height = compact ? 150 : 218;
        CoverViewport.Width = compact ? 104 : 156;
        CoverViewport.Height = compact ? 146 : 214;
        DetailTitleText.FontSize = compact ? 22 : 24;
        DetailTitleText.LineHeight = compact ? 30 : 32;
        DetailSubtitleText.FontSize = 16;
        HeroEyebrow.Margin = new Thickness(0, 0, 0, 4);
        HeroActions.Margin = new Thickness(0, 10, 0, 0);
        SummarySurface.Margin = new Thickness(0, 8, 0, 0);
        SummarySurface.Padding = new Thickness(10, 6, 10, 6);
        DetailBody.Margin = compact ? new Thickness(20, 10, 20, 8) : new Thickness(24, 16, 24, 16);
        HistoryEyebrow.Visibility = Visibility.Collapsed;
        HistoryHeader.Margin = new Thickness(0, 0, 0, 12);
    }
    private void ArchiveLayout_Loaded(object sender, RoutedEventArgs e) => ArrangeArchive((Grid)sender);
    private void ArchiveLayout_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => ArrangeArchive((Grid)sender);
    private void ArchiveLayout_SizeChanged(object sender, SizeChangedEventArgs e) => ArrangeArchive((Grid)sender);
    private static void ArrangeArchive(Grid grid)
    {
        if (grid.ActualWidth <= 0) return;
        var stacked = grid.ActualWidth < 480;
        var ticket = grid.Children.OfType<Grid>().First(element => element.Name == "ArchiveTicket");
        var notes = grid.Children.OfType<FrameworkElement>().First(element => element.Name == "ArchiveNotes");
        grid.ColumnDefinitions[0].Width = stacked ? new GridLength(1, GridUnitType.Star) : new GridLength(112);
        grid.ColumnDefinitions[1].Width = new GridLength(stacked ? 0 : 24);
        grid.ColumnDefinitions[2].Width = stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumnSpan(ticket, stacked ? 3 : 1);
        Grid.SetRowSpan(ticket, stacked ? 1 : 2);
        ticket.Margin = new Thickness();
        var date = ticket.Children.OfType<TextBlock>().First(element => element.Name == "ArchiveDateText");
        var metadata = ticket.Children.OfType<StackPanel>().First(element => element.Name == "ArchiveDateMetadata");
        var edit = ticket.Children.OfType<Button>().First(element => element.Name == "ArchiveEditButton");
        var weekday = metadata.Children.OfType<TextBlock>().First(element => element.Name == "ArchiveWeekdayText");
        Grid.SetColumnSpan(date, stacked ? 1 : 3);
        date.Margin = new Thickness(0, stacked ? 2 : 6, 0, 0);
        Grid.SetRow(metadata, stacked ? 1 : 2);
        Grid.SetColumn(metadata, stacked ? 1 : 0);
        Grid.SetColumnSpan(metadata, stacked ? 1 : 3);
        metadata.Orientation = stacked ? Orientation.Horizontal : Orientation.Vertical;
        metadata.Margin = stacked ? new Thickness(12, 0, 12, 0) : new Thickness(0, 3, 0, 0);
        weekday.Margin = stacked ? new Thickness(8, 0, 0, 0) : new Thickness(0, 3, 0, 0);
        Grid.SetRow(edit, stacked ? 1 : 3);
        Grid.SetColumn(edit, stacked ? 2 : 0);
        Grid.SetColumnSpan(edit, stacked ? 1 : 3);
        edit.Margin = stacked ? new Thickness() : new Thickness(-8, 6, 0, 0);
        edit.HorizontalAlignment = stacked ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        foreach (var score in grid.Children.OfType<FrameworkElement>().Where(element => element.Name is "DimensionScores" or "IncompleteRating"))
        {
            Grid.SetRow(score, stacked ? 2 : 1);
            Grid.SetColumn(score, stacked ? 0 : 2);
            Grid.SetColumnSpan(score, stacked ? 3 : 1);
            score.Margin = new Thickness(0, 14, 0, 0);
            if (score.Name == "DimensionScores")
                score.Width = Math.Min(420, stacked ? grid.ActualWidth : Math.Max(0, grid.ActualWidth - 136));
        }
        Grid.SetRow(notes, stacked ? 1 : 0);
        Grid.SetColumn(notes, stacked ? 0 : 2);
        Grid.SetColumnSpan(notes, stacked ? 3 : 1);
        notes.Margin = stacked ? new Thickness(0, 12, 0, 0) : new Thickness();
    }
    private void PrimaryExperience_Click(object sender, RoutedEventArgs e) => CompletionRequested?.Invoke();
    private void EditWork_Click(object sender, RoutedEventArgs e) => EditRequested?.Invoke();
    private void DeleteWork_Click(object sender, RoutedEventArgs e) => DeleteRequested?.Invoke();
    private void ManageCovers_Click(object sender, RoutedEventArgs e) => CoversRequested?.Invoke();
    private void EditExperience_Click(object sender, RoutedEventArgs e)
    {
        var experience = (sender as FrameworkElement)?.Tag switch { MediaExperience value => value, ExperienceCard card => card.Value, _ => null };
        if (experience is not null) ExperienceRequested?.Invoke(experience);
    }
}
