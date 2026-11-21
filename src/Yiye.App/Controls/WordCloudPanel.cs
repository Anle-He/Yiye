using System.Windows;
using System.Windows.Controls;

namespace Yiye.Controls;

public sealed class WordCloudPanel : Panel
{
    private const double Gap = 5;
    private const double Step = 4;
    public int VisibleCount { get; private set; }
    public static readonly RoutedEvent VisibleCountChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(VisibleCountChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(WordCloudPanel));
    public event RoutedEventHandler VisibleCountChanged
    {
        add => AddHandler(VisibleCountChangedEvent, value);
        remove => RemoveHandler(VisibleCountChangedEvent, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 320;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : 156;
        foreach (UIElement child in InternalChildren)
            child.Measure(new Size(Math.Max(0, width - Gap * 2), height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var placements = new List<Rect>();
        foreach (var anchor in new[] { 0.5, 0.33, 0.67 })
        {
            var candidate = new List<Rect>();
            foreach (UIElement child in InternalChildren)
            {
                var size = new Size(Math.Min(child.DesiredSize.Width, Math.Max(0, finalSize.Width - Gap * 2)), child.DesiredSize.Height);
                // Keep a ranking prefix when the available space fills up.
                if (FindPosition(size, finalSize, candidate, anchor) is not { } position) break;
                candidate.Add(position);
            }
            if (candidate.Count > placements.Count) placements = candidate;
            if (placements.Count == InternalChildren.Count) break;
        }

        var offset = new Vector();
        if (placements.Count > 0)
        {
            var bounds = placements.Aggregate(Rect.Empty, (all, next) => Rect.Union(all, next));
            offset = new Vector((finalSize.Width - bounds.Width) / 2 - bounds.Left,
                (finalSize.Height - bounds.Height) / 2 - bounds.Top);
        }
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];
            // Hidden elements retain their measured size so a larger window can reveal them.
            child.Visibility = index < placements.Count ? Visibility.Visible : Visibility.Hidden;
            if (index < placements.Count)
            {
                var bounds = placements[index];
                bounds.Offset(offset);
                child.Arrange(bounds);
            }
            else child.Arrange(new Rect(0, 0, 0, 0));
        }
        VisibleCount = placements.Count;
        RaiseEvent(new RoutedEventArgs(VisibleCountChangedEvent));
        return finalSize;
    }

    private static Rect? FindPosition(Size word, Size area, IReadOnlyList<Rect> occupied, double anchor)
    {
        if (word.Width <= 0 || word.Height <= 0 || word.Width > area.Width || word.Height > area.Height) return null;
        Rect? best = null;
        var bestDistance = double.PositiveInfinity;
        var maxX = area.Width - word.Width;
        var maxY = area.Height - word.Height;
        for (double y = 0; y <= maxY; y += Step)
        for (double x = 0; x <= maxX; x += Step)
        {
            var candidate = new Rect(new Point(x, y), word);
            var padded = candidate;
            padded.Inflate(Gap / 2, Gap / 2);
            if (occupied.Any(bounds => bounds.IntersectsWith(padded))) continue;
            var dx = candidate.Left + word.Width / 2 - area.Width * (occupied.Count == 0 ? anchor : 0.5);
            var dy = candidate.Top + word.Height / 2 - area.Height / 2;
            var distance = dx * dx + dy * dy * 1.15;
            if (distance >= bestDistance) continue;
            best = candidate;
            bestDistance = distance;
        }
        return best;
    }
}
