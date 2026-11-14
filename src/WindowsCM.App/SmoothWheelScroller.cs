// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsCM.Core.Popup;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;

namespace WindowsCM.App;

// The wheel for every popup card list (compact and large, vertical and
// horizontal): a distance proportional to the delta, animated, advanced
// once per presented frame.
//
// Before, the compact popup moved a fixed 32 DIPs per event, the large
// vertical one 48 (WPF's default), both ignoring the delta and jumping. The
// large horizontal one animated, but advanced on every Rendering callback:
// WPF raises it several times per frame, and each offset change invalidated
// the layout and raised it again — ~5,000 offset changes and ~10,000 layout
// passes a second, the UI thread at ~90% CPU for ~160 real frames.
internal sealed class SmoothWheelScroller
{
    private readonly ItemsControl _list;
    private readonly Func<bool> _isHorizontal;
    private readonly Func<double> _horizontalDipsPerNotch;
    private readonly SmoothScrollController _controller = new();
    private ScrollViewer? _viewer;
    private bool _hooked;
    private bool _horizontal;
    private TimeSpan _lastFrame;
    private double _lastWritten = double.NaN;

    public SmoothWheelScroller(ItemsControl list, Func<bool> isHorizontal, Func<double> horizontalDipsPerNotch)
    {
        _list = list;
        _isHorizontal = isHorizontal;
        _horizontalDipsPerNotch = horizontalDipsPerNotch;
    }

    public bool IsAnimating => _hooked;

    public void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        var viewer = Viewer();
        if (viewer is null)
        {
            return;
        }
        e.Handled = true;
        var horizontal = _isHorizontal();
        if (horizontal != _horizontal)
        {
            Stop();
            _horizontal = horizontal;
        }
        var offset = horizontal ? viewer.HorizontalOffset : viewer.VerticalOffset;
        var max = horizontal ? viewer.ScrollableWidth : viewer.ScrollableHeight;
        var perNotch = horizontal
            ? _horizontalDipsPerNotch()
            : WheelScrollStep.VerticalPerNotch(SystemParameters.WheelScrollLines, viewer.ViewportHeight);
        if (!_hooked)
        {
            // Idle: start from where the list is (the scrollbar, keys or a
            // reset may have moved it since the last wheel).
            _controller.SetImmediate(offset, max);
        }
        _controller.AddDelta(WheelScrollStep.Distance(e.Delta, perNotch), max);
        if (_controller.IsAnimating && !_hooked)
        {
            _hooked = true;
            _lastFrame = TimeSpan.Zero;
            _lastWritten = offset;
            CompositionTarget.Rendering += OnRendering;
        }
    }

    // A reset, a keyboard move or a click owns the offset from now on.
    public void Stop()
    {
        if (_hooked)
        {
            CompositionTarget.Rendering -= OnRendering;
            _hooked = false;
        }
        _lastWritten = double.NaN;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        // Several callbacks arrive per presented frame: advance on the first.
        var frame = e is RenderingEventArgs rendering ? rendering.RenderingTime : TimeSpan.Zero;
        if (frame == _lastFrame && _lastFrame != TimeSpan.Zero)
        {
            return;
        }
        var dt = _lastFrame == TimeSpan.Zero ? 1 / 60.0 : (frame - _lastFrame).TotalSeconds;
        _lastFrame = frame;
        var viewer = Viewer();
        if (viewer is null || !_list.IsVisible)
        {
            Stop();
            return;
        }
        var actual = _horizontal ? viewer.HorizontalOffset : viewer.VerticalOffset;
        var max = _horizontal ? viewer.ScrollableWidth : viewer.ScrollableHeight;
        if (Math.Abs(actual - _lastWritten) > 1)
        {
            // The panel re-estimated its extent or clamped the last write.
            _controller.Rebase(actual, max);
        }
        var animating = _controller.Tick(Math.Clamp(dt, 0, 0.05));
        var next = _controller.CurrentOffset;
        if (Math.Abs(next - actual) >= 0.01)
        {
            if (_horizontal)
            {
                viewer.ScrollToHorizontalOffset(next);
            }
            else
            {
                viewer.ScrollToVerticalOffset(next);
            }
        }
        _lastWritten = next;
        if (!animating)
        {
            Stop();
        }
    }

    private ScrollViewer? Viewer()
    {
        if (_viewer is not null && _viewer.IsDescendantOf(_list))
        {
            return _viewer;
        }
        _viewer = FindDescendant(_list);
        return _viewer;
    }

    private static ScrollViewer? FindDescendant(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }
            if (FindDescendant(child) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }
}
