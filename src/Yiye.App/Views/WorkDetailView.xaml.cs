using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Yiye.Operations;
using Yiye.Models;
using Yiye.Presentation;
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
