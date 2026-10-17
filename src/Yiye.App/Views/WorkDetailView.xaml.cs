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
        DetailKickerText.Text = work.Kind == "book" ? "书籍档案" : "影视档案";
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
            ? $"{work.KindLabel} · 尚无完成记录"
            : $"{work.KindLabel} · {work.ExperienceSummaryLabel} · {work.LatestActivityLabel}";
        DetailRankText.Text = work.AggregateRankLabel;
        DetailCountText.Text = work.ExperienceCountLabel;
        DetailRatingCountText.Text = work.RatingCountLabel;
        PrimaryExperienceButton.Content = "记录一次完成";

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
        IsCompact = e.NewSize.Width < 1000 || e.NewSize.Height < 700;
        var compact = IsCompact;
        HeroSurface.Padding = new Thickness(compact ? 12 : 16);
        HeroEyebrow.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        HeroMeta.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        DetailHero.MinHeight = compact ? 146 : 218;
        CoverColumn.Width = new GridLength(compact ? 112 : 160);
        CoverGap.Width = new GridLength(compact ? 16 : 28);
        DetailCoverButton.Width = compact ? 108 : 156;
        DetailCoverButton.Height = compact ? 150 : 218;
        CoverViewport.Width = compact ? 104 : 152;
        CoverViewport.Height = compact ? 146 : 214;
        Grid.SetRowSpan(DetailCoverButton, compact ? 2 : 1);
        Grid.SetColumnSpan(HeroInformation, compact ? 2 : 1);
        Grid.SetRow(HeroActions, compact ? 1 : 0);
        Grid.SetColumn(HeroActions, compact ? 2 : 3);
        Grid.SetColumnSpan(HeroActions, compact ? 2 : 1);
        HeroActions.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        HeroActions.Margin = compact ? new Thickness(0, 8, 0, 0) : new Thickness();
        SummarySurface.Padding = compact ? new Thickness(16, 8, 16, 8) : new Thickness(22, 10, 22, 10);
        DetailBody.Margin = compact ? new Thickness(16, 8, 16, 8) : new Thickness(24, 8, 24, 8);
        HistoryEyebrow.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        HistoryTitle.Margin = compact ? new Thickness() : new Thickness(0, 5, 0, 0);
        HistoryHeader.Margin = compact ? new Thickness(2, 8, 2, 4) : new Thickness(2, 16, 2, 8);
    }
    private void ArchiveLayout_Loaded(object sender, RoutedEventArgs e) => ArrangeArchive((Grid)sender);
    private void ArchiveLayout_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => ArrangeArchive((Grid)sender);
    private void ArchiveLayout_SizeChanged(object sender, SizeChangedEventArgs e) => ArrangeArchive((Grid)sender);
    private static void ArrangeArchive(Grid grid)
    {
        if (grid.ActualWidth <= 0) return;
        var narrow = grid.ActualWidth < 760;
        var stacked = grid.ActualWidth < 530;
        var ticket = grid.Children.OfType<FrameworkElement>().First(element => element.Name == "ArchiveTicket");
        var notes = grid.Children.OfType<FrameworkElement>().First(element => element.Name == "ArchiveNotes");
        grid.ColumnDefinitions[0].MinWidth = stacked ? 0 : narrow ? 220 : 270;
        grid.ColumnDefinitions[2].MinWidth = stacked ? 0 : 245;
        grid.ColumnDefinitions[0].MaxWidth = stacked ? double.PositiveInfinity : 352;
        Grid.SetColumnSpan(ticket, stacked ? 3 : 1);
        ticket.Margin = new Thickness(0, 0, narrow ? 12 : 18, 0);
        foreach (var score in grid.Children.OfType<FrameworkElement>().Where(element => element.Name is "DimensionScores" or "IncompleteRating"))
        {
            Grid.SetRow(score, stacked ? 1 : 0);
            Grid.SetColumn(score, stacked ? 0 : 2);
            Grid.SetColumnSpan(score, stacked ? 3 : 1);
            score.Margin = stacked ? new Thickness(0, 14, 0, 0) : new Thickness();
        }
        Grid.SetRow(notes, narrow ? stacked ? 2 : 1 : 0);
        Grid.SetColumn(notes, narrow ? 0 : 1);
        Grid.SetColumnSpan(notes, narrow ? 3 : 1);
        notes.Margin = narrow ? new Thickness(0, 8, 0, 0) : new Thickness(12, 0, 14, 0);
        notes.Visibility = narrow && grid.DataContext is ExperienceArchiveCard { HasNotes: false } ? Visibility.Collapsed : Visibility.Visible;
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
