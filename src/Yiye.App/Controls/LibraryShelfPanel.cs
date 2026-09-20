using System.Windows;
using System.Windows.Controls;

namespace Yiye.Controls;

/// <summary>Fits equal-width cover tiles to the available shelf width.</summary>
public sealed class LibraryShelfPanel : Panel
{
    private const double MinimumTileWidth = 168;
    private double _rowHeight;

    private static int Columns(double width) => Math.Max(1, (int)(width / MinimumTileWidth));

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : MinimumTileWidth;
        var columns = Columns(width);
        _rowHeight = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(width / columns, double.PositiveInfinity));
            _rowHeight = Math.Max(_rowHeight, child.DesiredSize.Height);
        }
        return new Size(width, Math.Ceiling((double)InternalChildren.Count / columns) * _rowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Columns(finalSize.Width);
        var width = finalSize.Width / columns;
        for (var index = 0; index < InternalChildren.Count; index++)
            InternalChildren[index].Arrange(new Rect(index % columns * width,
                index / columns * _rowHeight, width, _rowHeight));
        return finalSize;
    }
}
