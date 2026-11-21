using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yiye.Presentation;

namespace Yiye.Controls;

public sealed class BalancedTitleBlock : TextBlock
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(BalancedTitleBlock),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnTypographyChanged));

    static BalancedTitleBlock()
    {
        foreach (var property in new[] { FontFamilyProperty, FontSizeProperty, FontStyleProperty,
            FontWeightProperty, FontStretchProperty, LanguageProperty, FlowDirectionProperty,
            PaddingProperty, TextOptions.TextFormattingModeProperty })
            property.OverrideMetadata(typeof(BalancedTitleBlock), new FrameworkPropertyMetadata(OnTypographyChanged));
    }

    public BalancedTitleBlock()
    {
        SizeChanged += (_, _) => UpdateText();
        Loaded += (_, _) => UpdateText();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private static void OnTypographyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        => ((BalancedTitleBlock)sender).UpdateText();

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateText();
    }

    private void UpdateText()
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var culture = Language.GetSpecificCulture();
        var formattingMode = TextOptions.GetTextFormattingMode(this);
        double MeasureWidth(string value) => new FormattedText(value, culture, FlowDirection,
            typeface, FontSize, Brushes.Black, null, formattingMode, pixelsPerDip).WidthIncludingTrailingWhitespace;

        var availableWidth = ActualWidth - Padding.Left - Padding.Right;
        var displayText = TitleLineBalancer.Balance(Title ?? string.Empty, availableWidth, MeasureWidth);
        if (Text != displayText) Text = displayText;
    }
}
