// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsCM.Core.Settings;

namespace WindowsCM.App;

public static class ScrollbarPositionHelper
{
    public static void ApplyPositions(
        DependencyObject? root,
        VerticalScrollbarPosition verticalPos,
        HorizontalScrollbarPosition horizontalPos)
    {
        if (root == null) return;

        var scrollViewer = root as ScrollViewer ?? FindDescendant<ScrollViewer>(root);
        if (scrollViewer == null) return;

        if (!scrollViewer.IsLoaded)
        {
            RoutedEventHandler? onLoaded = null;
            onLoaded = (s, e) =>
            {
                scrollViewer.Loaded -= onLoaded;
                ApplyToScrollViewer(scrollViewer, verticalPos, horizontalPos);
            };
            scrollViewer.Loaded += onLoaded;
        }

        ApplyToScrollViewer(scrollViewer, verticalPos, horizontalPos);
    }

    public static void ApplyToScrollViewer(
        ScrollViewer scrollViewer,
        VerticalScrollbarPosition verticalPos,
        HorizontalScrollbarPosition horizontalPos)
    {
        if (VisualTreeHelper.GetChildrenCount(scrollViewer) == 0)
        {
            scrollViewer.ApplyTemplate();
        }

        if (VisualTreeHelper.GetChildrenCount(scrollViewer) == 0) return;

        var grid = VisualTreeHelper.GetChild(scrollViewer, 0) as Grid;
        if (grid == null) return;

        var presenter = scrollViewer.Template.FindName("PART_ScrollContentPresenter", scrollViewer) as FrameworkElement;
        var vScrollBar = scrollViewer.Template.FindName("PART_VerticalScrollBar", scrollViewer) as FrameworkElement;
        var hScrollBar = scrollViewer.Template.FindName("PART_HorizontalScrollBar", scrollViewer) as FrameworkElement;

        // Vertical scrollbar: Left vs Right
        if (grid.ColumnDefinitions.Count >= 2)
        {
            if (verticalPos == VerticalScrollbarPosition.Left)
            {
                grid.ColumnDefinitions[0].Width = GridLength.Auto;
                grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

                if (vScrollBar != null) Grid.SetColumn(vScrollBar, 0);
                if (presenter != null) Grid.SetColumn(presenter, 1);
                if (hScrollBar != null) Grid.SetColumn(hScrollBar, 1);
            }
            else // Right (Default)
            {
                grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                grid.ColumnDefinitions[1].Width = GridLength.Auto;

                if (presenter != null) Grid.SetColumn(presenter, 0);
                if (hScrollBar != null) Grid.SetColumn(hScrollBar, 0);
                if (vScrollBar != null) Grid.SetColumn(vScrollBar, 1);
            }
        }

        // Horizontal scrollbar: Top vs Bottom
        if (grid.RowDefinitions.Count >= 2)
        {
            if (horizontalPos == HorizontalScrollbarPosition.Top)
            {
                grid.RowDefinitions[0].Height = GridLength.Auto;
                grid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);

                if (hScrollBar != null) Grid.SetRow(hScrollBar, 0);
                if (presenter != null) Grid.SetRow(presenter, 1);
                if (vScrollBar != null) Grid.SetRow(vScrollBar, 1);
            }
            else // Bottom (Default)
            {
                grid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                grid.RowDefinitions[1].Height = GridLength.Auto;

                if (presenter != null) Grid.SetRow(presenter, 0);
                if (vScrollBar != null) Grid.SetRow(vScrollBar, 0);
                if (hScrollBar != null) Grid.SetRow(hScrollBar, 1);
            }
        }
    }

    private static T? FindDescendant<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;
            var found = FindDescendant<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
