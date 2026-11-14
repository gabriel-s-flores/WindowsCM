// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;
using Panel = System.Windows.Controls.Panel;

namespace WindowsCM.App;

// Updates the cards a background result belongs to (a decoded thumbnail, a
// favicon, a file probe), and only those.
//
// Each result used to refresh the whole list (Items.Refresh), which threw
// away and rebuilt every realized card: 55 ms median, 85 ms max per refresh,
// several times while a popup opened, and during a scroll it turned smooth
// frames into visible stalls. Now the result names its key (a path or a
// domain), and the realized cards that read that key re-evaluate their
// bindings in place: no template is rebuilt.
internal sealed class CardRefreshQueue
{
    private readonly Window _window;
    private readonly ItemsControl _list;
    private readonly DispatcherTimer _timer;
    private readonly HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public CardRefreshQueue(Window window, ItemsControl list)
    {
        _window = window;
        _list = list;
        // Results stream in while a popup opens: one pass per burst.
        _timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _timer.Tick += (_, _) => Flush();
        CardFileFacts.Updated += Enqueue;
        ImageThumbnailCache.Updated += Enqueue;
        FaviconService.FaviconUpdated += Enqueue;
    }

    // Thread-safe: results land on worker threads.
    private void Enqueue(string key)
    {
        lock (_gate)
        {
            if (_keys.Count < 1024)
            {
                _keys.Add(key);
            }
        }
        _window.Dispatcher.BeginInvoke(() =>
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        });
    }

    private void Flush()
    {
        _timer.Stop();
        string[] keys;
        lock (_gate)
        {
            keys = [.. _keys];
            _keys.Clear();
        }
        // A hidden popup rebuilds its cards when shown.
        if (keys.Length == 0 || !_window.IsVisible)
        {
            return;
        }
        using var stage = Core.Diagnostics.UiActivity.Shared.Enter("card-refresh");
        var changed = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        RefreshRealized(_list, item => DependsOn(item, changed));
    }

    // The keys a card's converters look up (see PopupConverters): the file
    // facts of its first path, the thumbnail of its image or link preview,
    // its link's favicon domain.
    internal static bool DependsOn(ClipboardItem item, IReadOnlySet<string> keys)
    {
        switch (item.Kind)
        {
            case ItemKind.File:
            case ItemKind.Files:
                return Contains(keys, CardFileFacts.KeyFor(TextPreview.FirstNonEmptySegment(item.Content)))
                    || (MediaItemClassifier.IsAudioItem(item, out var audio) && Contains(keys, CardFileFacts.KeyFor(audio)))
                    || (MediaItemClassifier.IsVideoItem(item, out var video) && Contains(keys, CardFileFacts.KeyFor(video)));
            case ItemKind.Image:
                return Contains(keys, ItemDisplayFormatter.TryGetLocalImagePath(item));
            case ItemKind.Link:
                return Contains(keys, LinkPreviewImages.LocalPath(item))
                    || Contains(keys, LinkDisplayHelper.GetDomain(item.Content));
            default:
                return false;
        }
    }

    private static bool Contains(IReadOnlySet<string> keys, string? key) =>
        !string.IsNullOrEmpty(key) && keys.Contains(key);

    // Re-evaluates the bindings of the realized cards whose item matches.
    // Returns how many cards were updated.
    internal static int RefreshRealized(ItemsControl list, Func<ClipboardItem, bool> affected)
    {
        var generator = list.ItemContainerGenerator;
        var panel = ItemsHost(list);
        if (panel is null)
        {
            return 0;
        }
        var updated = 0;
        foreach (UIElement child in panel.Children)
        {
            if (generator.ItemFromContainer(child) is not ClipboardItem item || !affected(item))
            {
                continue;
            }
            if (TemplateRoot(child) is { } root)
            {
                // The card's bindings all read the item itself through
                // converters ({Binding Converter=...}); an item never changes
                // in place, so nothing notifies them. A DataContext round
                // trip makes each binding run its converter again.
                root.DataContext = null;
                root.ClearValue(FrameworkElement.DataContextProperty);
                updated++;
            }
        }
        return updated;
    }

    private static Panel? ItemsHost(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Panel { IsItemsHost: true } panel)
            {
                return panel;
            }
            if (ItemsHost(child) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }

    // The root of the card's DataTemplate: the first child of the
    // container's ContentPresenter.
    private static FrameworkElement? TemplateRoot(DependencyObject container)
    {
        var count = VisualTreeHelper.GetChildrenCount(container);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(container, i);
            if (child is ContentPresenter presenter)
            {
                return VisualTreeHelper.GetChildrenCount(presenter) > 0
                    ? VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement
                    : null;
            }
            if (TemplateRoot(child) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }
}
