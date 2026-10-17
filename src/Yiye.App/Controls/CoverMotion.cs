using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Yiye.Presentation;

namespace Yiye.Controls;

// A temporary copy of the same cover joins the two layouts during navigation.
public sealed class CoverMotion
{
    private FrameworkElement? _destination;
    private Canvas? _layer;

    public static Image? Find(DependencyObject root, string workId)
    {
        if (root is Image { Source: not null, IsVisible: true } image && image.DataContext switch
        {
            WorkCard card => card.Id == workId,
            ShowcaseCard card => card.WorkId == workId,
            TimelineCard card => card.WorkId == workId,
            LibraryMonthPreview preview => preview.Work.Id == workId,
            _ => false
        }) return image;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (Find(VisualTreeHelper.GetChild(root, i), workId) is { } found) return found;
        return null;
    }

    public static Rect? Bounds(FrameworkElement? element, Visual relativeTo)
    {
        if (element is null || !element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) return null;
        var bounds = new Rect(element.TranslatePoint(new Point(), (UIElement)relativeTo), element.RenderSize);
        return bounds.IntersectsWith(new Rect(new Point(), ((FrameworkElement)relativeTo).RenderSize)) ? bounds : null;
    }

    public void Cancel()
    {
        if (_destination is not null) _destination.Opacity = 1;
        _destination = null;
        _layer?.Children.Clear();
        _layer = null;
    }

    public void Play(Canvas layer, Rect? from, FrameworkElement? destination, ImageSource? source)
    {
        Cancel();
        if (!SystemParameters.ClientAreaAnimation || from is not { } start || source is null
            || Bounds(destination, layer) is not { } end) return;
        _layer = layer;
        _destination = destination;
        destination!.Opacity = 0;
        var image = new Image { Source = source, Stretch = Stretch.Uniform, Width = start.Width, Height = start.Height };
        Canvas.SetLeft(image, start.Left);
        Canvas.SetTop(image, start.Top);
        layer.Children.Add(image);
        var duration = TimeSpan.FromMilliseconds(240);
        DoubleAnimation Move(double a, double b) => new(a, b, duration)
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
        var finish = Move(start.Left, end.Left);
        finish.Completed += (_, _) => { if (_layer == layer && layer.Children.Contains(image)) Cancel(); };
        image.BeginAnimation(Canvas.LeftProperty, finish);
        image.BeginAnimation(Canvas.TopProperty, Move(start.Top, end.Top));
        image.BeginAnimation(FrameworkElement.WidthProperty, Move(start.Width, end.Width));
        image.BeginAnimation(FrameworkElement.HeightProperty, Move(start.Height, end.Height));
    }
}
