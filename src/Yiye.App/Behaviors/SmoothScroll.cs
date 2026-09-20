using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Yiye.Behaviors;

public static class SmoothScroll
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SmoothScroll), new PropertyMetadata(false, OnEnabledChanged));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(State), typeof(SmoothScroll));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not FrameworkElement viewer) return;
        if (viewer.GetValue(StateProperty) is State previous) previous.Detach();
        viewer.SetValue(StateProperty, (bool)args.NewValue ? new State(viewer) : null);
    }

    private sealed class State
    {
        private readonly FrameworkElement _owner;
        private ScrollViewer? _viewer;
        private readonly Stopwatch _clock = new();
        private double _start;
        private double _target;

        public State(FrameworkElement viewer)
        {
            _owner = viewer;
            viewer.PreviewMouseWheel += OnWheel;
            viewer.PreviewKeyDown += OnKeyDown;
            viewer.Unloaded += OnUnloaded;
            viewer.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
        }

        public void Detach()
        {
            Stop();
            _owner.PreviewMouseWheel -= OnWheel;
            _owner.PreviewKeyDown -= OnKeyDown;
            _owner.Unloaded -= OnUnloaded;
            _owner.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
        }

        private void OnWheel(object sender, MouseWheelEventArgs args)
        {
            // Keep wheel input inside the collection, including at its boundaries.
            _viewer = FindViewer(_owner);
            if (_viewer is null) return;
            args.Handled = true;
            if (_viewer.ScrollableHeight <= 0 || SystemParameters.WheelScrollLines == 0) return;
            var step = SystemParameters.WheelScrollLines < 0
                ? _viewer.ViewportHeight : SystemParameters.WheelScrollLines * 16d;
            var target = Math.Clamp((_clock.IsRunning ? _target : _viewer.VerticalOffset)
                - args.Delta / 120d * step, 0, _viewer.ScrollableHeight);
            Stop();
            if (!SystemParameters.ClientAreaAnimation)
            {
                _viewer.ScrollToVerticalOffset(target);
                return;
            }
            _start = _viewer.VerticalOffset;
            _target = target;
            if (Math.Abs(_target - _start) < 0.01) return;
            _clock.Restart();
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnRendering(object? sender, EventArgs args)
        {
            var progress = Math.Min(_clock.Elapsed.TotalMilliseconds / 160d, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            _viewer!.ScrollToVerticalOffset(_start + (_target - _start) * eased);
            if (progress >= 1) Stop();
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs args)
        {
            if (args.ExtentHeightChange != 0 || args.ViewportHeightChange != 0) Stop();
        }

        private static ScrollViewer? FindViewer(DependencyObject element)
        {
            if (element is ScrollViewer viewer) return viewer;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
                if (FindViewer(VisualTreeHelper.GetChild(element, index)) is { } child) return child;
            return null;
        }

        private void OnKeyDown(object sender, KeyEventArgs args) => Stop();
        private void OnUnloaded(object sender, RoutedEventArgs args) => Stop();

        private void Stop()
        {
            CompositionTarget.Rendering -= OnRendering;
            _clock.Reset();
        }
    }
}
