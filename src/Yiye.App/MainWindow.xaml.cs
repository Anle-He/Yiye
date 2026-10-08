using System.Windows;
using System.Windows.Media.Animation;
using Yiye.Operations;
using Yiye.Data;
using Yiye.Models;
namespace Yiye;

public enum LibraryPageKind { Home, Library, Detail }

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly LibraryApplication _library;
    private readonly List<MediaWork> _works = [];
    private int _navigationVersion;
    private string? _selectedWorkId;
    private bool _initialized;
    private bool _busy;
    public Views.DashboardView HomeView => DashboardPage;
    public Views.LibraryView LibraryView => LibraryPage;
    public Views.WorkDetailView DetailView => DetailPage;
    public LibraryPageKind CurrentPage { get; private set; } = LibraryPageKind.Home;
    public MainWindow() : this(new LibraryApplication(new Database())) { }
    public MainWindow(LibraryApplication library)
    {
        _library = library;
        InitializeComponent();
        DashboardPage.AddRequested += AddWork;
        DashboardPage.WorkRequested += OpenWork;
        LibraryPage.AddRequested += AddWork;
        LibraryPage.WorkRequested += OpenWork;
        DetailPage.CompletionRequested += AddCompletion;
        DetailPage.ExperienceRequested += EditExperience;
        DetailPage.EditRequested += EditWork;
        DetailPage.DeleteRequested += DeleteWork;
        DetailPage.CoversRequested += ManageCovers;
        Loaded += async (_, _) => await RunAsync(InitializeLibraryAsync, "无法打开本地作品库");
        Closed += (_, _) => _navigationVersion++;
    }
    public async Task InitializeLibraryAsync()
    {
        if (_initialized) return;
        await _library.InitializeAsync();
        _works.Clear();
        _works.AddRange(await _library.Works.GetWorksAsync());
        LibraryPage.SetWorks(_works);
        DashboardPage.Render(_works, await _library.LoadDashboardAsync());
        _initialized = true;
    }
    public void ShowHome() => Navigate(LibraryPageKind.Home);
    public void ShowLibrary()
    {
        Navigate(LibraryPageKind.Library);
        LibraryPage.FocusSearch();
    }
    private void Navigate(LibraryPageKind page)
    {
        _navigationVersion++;
        CurrentPage = page;
        _selectedWorkId = null;
        DetailPage.Clear();
        SetPageVisibility(page);
    }
    private void SetPageVisibility(LibraryPageKind page)
    {
        DashboardPage.Visibility = page == LibraryPageKind.Home ? Visibility.Visible : Visibility.Collapsed;
        LibraryPage.Visibility = page == LibraryPageKind.Library ? Visibility.Visible : Visibility.Collapsed;
        DetailPage.Visibility = page == LibraryPageKind.Detail ? Visibility.Visible : Visibility.Collapsed;
        HomeButton.Appearance = page == LibraryPageKind.Home ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Transparent;
        LibraryButton.Appearance = page == LibraryPageKind.Library ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Transparent;
        if (IsLoaded && SystemParameters.ClientAreaAnimation)
        {
            FrameworkElement view = page switch { LibraryPageKind.Home => DashboardPage, LibraryPageKind.Library => LibraryPage, _ => DetailPage };
            view.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(140))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }
    }
    public async Task OpenWorkAsync(string workId)
    {
        var version = ++_navigationVersion;
        _selectedWorkId = workId;
        CurrentPage = LibraryPageKind.Detail;
        DetailPage.Clear();
        SetPageVisibility(CurrentPage);
        var detail = await _library.LoadWorkAsync(workId);
        if (version != _navigationVersion || _selectedWorkId != workId) return;
        if (detail is null) { ShowLibrary(); return; }
        DetailPage.Render(detail);
    }
    public async Task ApplyChangeAsync(WorkChange change)
    {
        var index = _works.FindIndex(work => work.Id == change.WorkId);
        if (change.Work is null) { if (index >= 0) _works.RemoveAt(index); }
        else if (index >= 0) _works[index] = change.Work;
        else _works.Add(change.Work);
        LibraryPage.SetWorks(_works);
        DashboardPage.Render(_works, await _library.LoadDashboardAsync());
        if (CurrentPage == LibraryPageKind.Detail && _selectedWorkId == change.WorkId)
        {
            if (change.Kind == WorkChangeKind.Removed) ShowHome();
            else await OpenWorkAsync(change.WorkId);
        }
    }
    private void Home_Click(object sender, RoutedEventArgs e) => ShowHome();
    private void Library_Click(object sender, RoutedEventArgs e) => ShowLibrary();
    private async void OpenWork(string workId) => await RunAsync(() => OpenWorkAsync(workId), "无法打开作品");
    private async void AddWork()
    {
        if (!_initialized || _busy) return;
        var dialog = new AddWorkWindow { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Work is not { } work) return;
        var existing = _works.FirstOrDefault(value => value.Kind == work.Kind && string.Equals(value.Title, work.Title, StringComparison.CurrentCultureIgnoreCase));
        if (existing is not null)
        {
            var choice = MessageBox.Show(this, $"《{existing.Title}》已经存在。\n\n选择“是”打开已有作品；选择“否”仍创建一个同名作品。",
                "已有同名作品", MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
            if (choice == MessageBoxResult.Yes) { await RunAsync(() => OpenWorkAsync(existing.Id), "无法打开作品"); return; }
            if (choice != MessageBoxResult.No) return;
        }
        await MutateAsync(async () =>
        {
            await ApplyChangeAsync(await _library.AddWorkAsync(work));
            await OpenWorkAsync(work.Id);
        }, "无法添加作品");
    }
    private async void AddCompletion()
    {
        if (_busy || DetailPage.Work is not { } work) return;
        var dialog = new AddExperienceWindow(work.Id, work.Kind) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Experience is not { } experience) return;
        await MutateAsync(async () => await ApplyChangeAsync(await _library.SaveCompletionAsync(experience, editing: false)), "无法保存本次记录");
    }
    private async void EditExperience(MediaExperience experience)
    {
        if (_busy || DetailPage.Work is not { } work) return;
        await MutateAsync(async () =>
        {
            var progress = await _library.Experiences.GetHistoricalProgressAsync(experience.Id);
            var dialog = new AddExperienceWindow(work.Id, work.Kind, experience, progress) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            if (dialog.DeleteRequested) await ApplyChangeAsync(await _library.DeleteExperienceAsync(experience));
            else if (dialog.Experience is { } edited) await ApplyChangeAsync(await _library.SaveCompletionAsync(edited, editing: true));
        }, "无法处理本次记录", disableWindow: false);
    }
    private async void EditWork()
    {
        if (_busy || DetailPage.Work is not { } work) return;
        var dialog = new AddWorkWindow(work) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Work is not { } edited) return;
        await MutateAsync(async () => await ApplyChangeAsync(await _library.EditWorkAsync(edited)), "无法更新作品");
    }
    private async void DeleteWork()
    {
        if (_busy || DetailPage.Work is not { } work) return;
        if (MessageBox.Show(this, $"删除《{work.Title}》？\n\n作品资料、全部阅读或观看记录、历史进度和封面都会一起删除。此操作无法撤销。",
            "删除作品", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await MutateAsync(async () => await ApplyChangeAsync(await _library.DeleteWorkAsync(work.Id)), "无法删除作品");
    }
    private async void ManageCovers()
    {
        if (_busy || DetailPage.Work is not { } work) return;
        var dialog = new ManageCoversWindow(_library.Covers, work) { Owner = this };
        dialog.ShowDialog();
        if (dialog.HasChanges)
            await MutateAsync(async () => await ApplyChangeAsync(await _library.CoversChangedAsync(work.Id)), "无法刷新封面");
    }
    private async Task MutateAsync(Func<Task> action, string errorTitle, bool disableWindow = true)
    {
        if (_busy) return;
        _busy = true;
        if (disableWindow) IsEnabled = false;
        try { await RunAsync(action, errorTitle); }
        finally { IsEnabled = true; _busy = false; }
    }
    private async Task RunAsync(Func<Task> action, string errorTitle)
    {
        try { await action(); }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
