// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WindowsCM.Core.Actions;
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Settings;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

using Key = System.Windows.Input.Key;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Separator = System.Windows.Controls.Separator;
using MessageBox = System.Windows.MessageBox;

namespace WindowsCM.App;

public partial class CompactPopupWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private readonly PopupViewModel _model;
    private readonly App _app;
    private bool _isActivating;
    private long _lastHideTimestamp;
    private readonly System.Windows.Threading.DispatcherTimer _cardRefreshTimer;
    private readonly System.Windows.Threading.DispatcherTimer _searchTimer;

    public CompactPopupWindow(PopupViewModel model, App app)
    {
        _model = model;
        _app = app;
        InitializeComponent();
        ItemsList.ItemsSource = _model.VisibleItems;
        // File facts (thumbnails, sizes, tags) land from a background probe:
        // one refresh per burst, like the large popup.
        _cardRefreshTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _cardRefreshTimer.Tick += (_, _) =>
        {
            _cardRefreshTimer.Stop();
            if (IsVisible)
            {
                ItemsList.Items.Refresh();
            }
        };
        // Each keystroke reloaded the history and rebuilt every visible card:
        // the search applies once typing pauses, and before any key that
        // acts on the list, so Enter never picks from a stale list.
        _searchTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Input, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(120),
        };
        _searchTimer.Tick += (_, _) => ApplyPendingSearch();
        CardFileFacts.Updated += RequestCardRefresh;
        ImageThumbnailCache.Updated += RequestCardRefresh;
        FaviconService.FaviconUpdated += _ => RequestCardRefresh();
    }

    // Thread-safe: file facts and thumbnails arrive from background work.
    private void RequestCardRefresh() =>
        Dispatcher.BeginInvoke(() =>
        {
            _cardRefreshTimer.Stop();
            _cardRefreshTimer.Start();
        });

    public bool WasRecentlyHidden => Environment.TickCount64 - _lastHideTimestamp < 350;

    public void ShowAtCursor(bool incognito)
    {
        ApplyLayoutOrientation(_app.Settings.Dialog.CompactOrientation);
        if (!_app.Settings.Behavior.RememberSearch && !string.IsNullOrEmpty(_model.SearchText))
        {
            _model.SetSearch("");
        }
        _model.Show(incognito);
        SearchBox.Text = _model.SearchText;
        UpdateSearchVisuals();
        RefreshView();
        _app.EnsureLinkPreviewsForRecentItems();

        PlaceNearCursor();
        ResetScrollToInitial();
        UpdateLayout();
        Show();

        var handle = new WindowInteropHelper(this).EnsureHandle();
        SetForegroundWindow(handle);
        Activate();
        SearchBox.Focus();
        if (!string.IsNullOrEmpty(SearchBox.Text))
        {
            SearchBox.SelectAll();
        }

        if (ItemsList.Items.Count > 0 && ItemsList.SelectedIndex < 0)
        {
            ItemsList.SelectedIndex = _model.SelectedIndex >= 0 ? _model.SelectedIndex : 0;
        }
    }

    public void ApplyLayoutOrientation(DialogOrientation orientation)
    {
        if (orientation == DialogOrientation.Horizontal)
        {
            Width = 540;
            Height = 240;

            if (TryFindResource("CompactHorizontalItemsPanelTemplate") is ItemsPanelTemplate hPanel)
            {
                ItemsList.ItemsPanel = hPanel;
            }
            if (TryFindResource("CompactHorizontalCardItemStyle") is Style hStyle)
            {
                ItemsList.ItemContainerStyle = hStyle;
            }
            ScrollViewer.SetHorizontalScrollBarVisibility(ItemsList, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(ItemsList, ScrollBarVisibility.Disabled);
        }
        else
        {
            Width = 320;
            Height = 480;

            if (TryFindResource("CompactVerticalItemsPanelTemplate") is ItemsPanelTemplate vPanel)
            {
                ItemsList.ItemsPanel = vPanel;
            }
            if (TryFindResource("CompactVerticalCardItemStyle") is Style vStyle)
            {
                ItemsList.ItemContainerStyle = vStyle;
            }
            ScrollViewer.SetHorizontalScrollBarVisibility(ItemsList, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(ItemsList, ScrollBarVisibility.Auto);
        }

        ApplyScrollbarPosition();
    }

    public void ApplyScrollbarPosition()
    {
        ScrollbarPositionHelper.ApplyPositions(
            ItemsList,
            _app.Settings.Dialog.VerticalScrollbarPosition,
            _app.Settings.Dialog.HorizontalScrollbarPosition);
    }

    public new void Hide()
    {
        if (_searchTimer.IsEnabled)
        {
            // Kept for RememberSearch; forgotten just below otherwise.
            ApplyPendingSearch();
        }
        _isActivating = false;
        _lastHideTimestamp = Environment.TickCount64;
        ResetScrollToInitial();
        base.Hide();
        // Only when there is a search to forget: SetSearch reloads the whole
        // history, and a paste hides both popups on its way to the target.
        if (!_app.Settings.Behavior.RememberSearch && !string.IsNullOrEmpty(_model.SearchText))
        {
            _model.SetSearch("");
        }
        if (!_app.Settings.Behavior.RememberSearch && SearchBox.Text.Length > 0)
        {
            SearchBox.Text = "";
        }
    }

    // Built once for the app's lifetime: Alt+F4 (or any WM_CLOSE) only hides
    // it. A closed window can never be shown again, so every later hotkey
    // threw ("Cannot ... after a Window has closed") until the burst limit
    // ended the app.
    private bool _closingForExit;

    internal void CloseForExit()
    {
        _closingForExit = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_closingForExit)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    public void ResetScrollToInitial()
    {
        var scrollViewer = FindVisualChild<ScrollViewer>(ItemsList);
        if (scrollViewer != null)
        {
            var orientation = _app.Settings.Dialog.CompactOrientation;
            var recentAtStart = orientation == DialogOrientation.Vertical
                ? _app.Settings.Dialog.CompactVerticalOrder == VerticalItemOrder.RecentOnTop
                : _app.Settings.Dialog.CompactHorizontalOrder == HorizontalItemOrder.RecentOnLeft;

            if (recentAtStart)
            {
                scrollViewer.ScrollToHorizontalOffset(0);
                scrollViewer.ScrollToVerticalOffset(0);
            }
            else
            {
                if (orientation == DialogOrientation.Horizontal)
                {
                    scrollViewer.ScrollToRightEnd();
                }
                else
                {
                    scrollViewer.ScrollToBottom();
                }
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;
            var found = FindVisualChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    public void RefreshView()
    {
        var orientation = _app.Settings.Dialog.CompactOrientation;
        var recentAtStart = orientation == DialogOrientation.Vertical
            ? _app.Settings.Dialog.CompactVerticalOrder == VerticalItemOrder.RecentOnTop
            : _app.Settings.Dialog.CompactHorizontalOrder == HorizontalItemOrder.RecentOnLeft;
        _model.SetItemOrdering(recentAtStart);

        ItemsList.ItemsSource = null;
        ItemsList.ItemsSource = _model.VisibleItems;
        if (ItemsList.Items.Count > 0 && ItemsList.SelectedIndex < 0)
        {
            ItemsList.SelectedIndex = _model.SelectedIndex >= 0 ? _model.SelectedIndex : 0;
        }
        if (_model.SelectedItem is not null)
        {
            ItemsList.ScrollIntoView(_model.SelectedItem);
        }
        UpdateIncognitoIndicator();
        ApplyScrollbarPosition();
    }

    private void PlaceNearCursor()
    {
        GetCursorPos(out var cursor);
        var handle = new WindowInteropHelper(this).EnsureHandle();
        var transform = HwndSource.FromHwnd(handle)?.CompositionTarget?.TransformFromDevice
            ?? PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        var (dx, dy) = (transform.M11, transform.M22);

        var cursorDips = (X: cursor.X * dx, Y: cursor.Y * dy);
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y))
                     ?? System.Windows.Forms.Screen.PrimaryScreen;

        if (screen is null)
        {
            Left = cursorDips.X;
            Top = cursorDips.Y;
            return;
        }

        var areaDips = new WorkArea(
            screen.WorkingArea.Left * dx,
            screen.WorkingArea.Top * dy,
            screen.WorkingArea.Right * dx,
            screen.WorkingArea.Bottom * dy);

        var (left, top, width, height) = PopupPlacement.PlaceCompactPopup(
            _app.Settings.Dialog.CompactOrientation,
            cursorDips.X,
            cursorDips.Y,
            areaDips);

        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    private ColorScheme _currentScheme = ColorScheme.Dark;
    public ColorScheme CurrentScheme => _currentScheme;

    public void ApplyTheme(ColorScheme scheme)
    {
        _currentScheme = scheme;
        var themeDict = PopupThemeBrushes.CreateThemeDictionary(scheme, _app.Settings.ItemColors);
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(themeDict);
        UpdateIncognitoIndicator();
    }

    public void ApplyTheme(bool isLight) =>
        ApplyTheme(isLight ? ColorScheme.Light : ColorScheme.Dark);


    private void UpdateIncognitoIndicator()
    {
        var isIncognito = _app.IsIncognito;
        if (isIncognito)
        {
            IncognitoButton.Background = TryFindResource("IncognitoActiveButtonBrush") as System.Windows.Media.Brush
                ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED));
            IncognitoButton.Foreground = System.Windows.Media.Brushes.White;
            IncognitoButton.BorderBrush = TryFindResource("IncognitoActiveButtonBorderBrush") as System.Windows.Media.Brush
                ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x93, 0x33, 0xEA));
            IncognitoButton.ToolTip = WindowsCM.Core.Localization.LocalizationManager.Strings.CompactIncognitoTooltipActive;

            if (IncognitoBanner is not null)
            {
                IncognitoBanner.Visibility = Visibility.Visible;
            }
            if (RootBorder is not null && TryFindResource("IncognitoBorderGlowBrush") is System.Windows.Media.Brush glow)
            {
                RootBorder.BorderBrush = glow;
            }
        }
        else
        {
            IncognitoButton.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
            IncognitoButton.ClearValue(System.Windows.Controls.Button.ForegroundProperty);
            IncognitoButton.ClearValue(System.Windows.Controls.Button.BorderBrushProperty);
            IncognitoButton.ToolTip = WindowsCM.Core.Localization.LocalizationManager.Strings.CompactIncognitoTooltipInactive;

            if (IncognitoBanner is not null)
            {
                IncognitoBanner.Visibility = Visibility.Collapsed;
            }
            if (RootBorder is not null && TryFindResource("PopupBorderBrush") is System.Windows.Media.Brush normalBorder)
            {
                RootBorder.BorderBrush = normalBorder;
            }
        }
    }

    private void OnExitIncognitoClicked(object sender, RoutedEventArgs e) =>
        _app.SetIncognito(false);

    private void UpdateSearchVisuals()
    {
        if (SearchPlaceholder is not null)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        if (ClearSearchButton is not null)
        {
            ClearSearchButton.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsVisible)
        {
            return;
        }
        UpdateSearchVisuals();
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void ApplyPendingSearch()
    {
        _searchTimer.Stop();
        if (_model.SearchText == SearchBox.Text)
        {
            return;
        }
        _model.SetSearch(SearchBox.Text);
        RefreshView();
    }

    private void OnClearSearchClicked(object sender, RoutedEventArgs e)
    {
        _searchTimer.Stop();
        SearchBox.Text = "";
        _model.SetSearch("");
        UpdateSearchVisuals();
        RefreshView();
        SearchBox.Focus();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshView();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!IsVisible || _isActivating)
        {
            return;
        }
        Hide();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsVisible)
        {
            return;
        }

        // With Alt held WPF reports Key.System and the real key in
        // SystemKey: Alt+P/S/C never matched.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_searchTimer.IsEnabled && PopupWindow.PendingSearchKeys(key))
        {
            ApplyPendingSearch();
        }

        if (key == Key.Escape)
        {
            e.Handled = true;
            Hide();
            return;
        }

        if (key is Key.Enter)
        {
            e.Handled = true;
            ActivateCurrentSelection();
            return;
        }

        if (key == Key.Down)
        {
            e.Handled = true;
            MoveSelection(1);
            return;
        }

        if (key == Key.Up)
        {
            e.Handled = true;
            MoveSelection(-1);
            return;
        }

        if (key == Key.PageDown)
        {
            e.Handled = true;
            MoveSelection(5);
            return;
        }

        if (key == Key.PageUp)
        {
            e.Handled = true;
            MoveSelection(-5);
            return;
        }

        if (key == Key.Delete)
        {
            if (ItemsList.IsKeyboardFocusWithin || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                DeleteSelected();
                return;
            }
        }

        if (key == Key.P && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            e.Handled = true;
            TogglePinSelected();
            return;
        }

        if (key == Key.S && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            e.Handled = true;
            OnSettingsClicked(this, new RoutedEventArgs());
            return;
        }

        if (key == Key.C && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            e.Handled = true;
            OnClearClicked(this, new RoutedEventArgs());
            return;
        }
    }

    private void MoveSelection(int delta)
    {
        if (ItemsList.Items.Count == 0) return;
        var current = ItemsList.SelectedIndex;
        var next = Math.Clamp(current + delta, 0, ItemsList.Items.Count - 1);
        if (next != current)
        {
            ItemsList.SelectedIndex = next;
            ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }
    }

    private void OnItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (!IsVisible || _isActivating)
        {
            return;
        }
        var isInteractive = VisualTreeWalk.FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(e.OriginalSource as DependencyObject) is not null;
        var index = RowIndexAt(e.OriginalSource as DependencyObject);
        if (!PopupClickPolicy.ShouldActivate(IsVisible, index, isInteractive))
        {
            return;
        }

        var shiftHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var request = _model.ActivateAt(index!.Value, runDefaultAction: false);
        if (request is not null)
        {
            StartActivation(request, shiftHeld);
        }
    }

    // A pick that fails before the popup hides (item gone, clipboard held
    // by another app) left _isActivating set: the popup then stayed on top,
    // ignored clicks and never hid on focus loss.
    private void StartActivation(ActivationRequest request, bool shiftHeld)
    {
        if (!shiftHeld)
        {
            _isActivating = true;
        }
        _ = ActivateThenSettleAsync(request, shiftHeld);
    }

    private async Task ActivateThenSettleAsync(ActivationRequest request, bool shiftHeld)
    {
        try
        {
            await _app.ActivateAsync(request, shiftHeld);
        }
        finally
        {
            _isActivating = false;
        }
    }

    private void OnItemDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        if (!IsVisible || _isActivating)
        {
            return;
        }
        ActivateCurrentSelection();
    }

    private void ActivateCurrentSelection()
    {
        var shiftHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var index = ItemsList.SelectedIndex;
        if (index < 0 || index >= ItemsList.Items.Count)
        {
            return;
        }

        var request = _model.ActivateAt(index, runDefaultAction: false);
        if (request is not null)
        {
            StartActivation(request, shiftHeld);
        }
    }

    private void OnItemsListPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = FindDescendant<ScrollViewer>(ItemsList);
        if (scrollViewer is null)
        {
            return;
        }
        if (_app.Settings.Dialog.CompactOrientation == DialogOrientation.Horizontal)
        {
            if (e.Delta < 0)
            {
                scrollViewer.LineRight();
                scrollViewer.LineRight();
            }
            else
            {
                scrollViewer.LineLeft();
                scrollViewer.LineLeft();
            }
        }
        else
        {
            if (e.Delta < 0)
            {
                scrollViewer.LineDown();
                scrollViewer.LineDown();
            }
            else
            {
                scrollViewer.LineUp();
                scrollViewer.LineUp();
            }
        }
        e.Handled = true;
    }

    private void OnCardPinButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardItem item })
        {
            TogglePinItem(item);
            e.Handled = true;
        }
    }

    private void OnCardDeleteButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardItem item })
        {
            DeleteItem(item);
            e.Handled = true;
        }
    }

    private void TogglePinSelected()
    {
        if (ItemsList.SelectedItem is ClipboardItem item)
        {
            TogglePinItem(item);
        }
    }

    private void DeleteSelected()
    {
        if (ItemsList.SelectedItem is ClipboardItem item)
        {
            DeleteItem(item);
        }
    }

    private void TogglePinItem(ClipboardItem item)
    {
        _app.Store.SetPinned(item.Id, !item.Pinned);
        _model.Refresh();
        RefreshView();
    }

    private void DeleteItem(ClipboardItem item)
    {
        _app.Store.Delete(item.Id);
        _model.Refresh();
        RefreshView();
    }

    private void OnCardQrButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardItem item })
        {
            _app.ShowQr(item);
            e.Handled = true;
        }
    }

    private void OnReceiveMobileClicked(object sender, RoutedEventArgs e)
    {
        _app.ShowMobileTransfer();
    }

    private void OnCardMenuButtonClicked(object sender, RoutedEventArgs e)
    {

        if (sender is FrameworkElement element && element.DataContext is ClipboardItem item)
        {
            ShowCardContextMenu(item, element);
            e.Handled = true;
        }
    }

    private void OnCardMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ClipboardItem item)
        {
            e.Handled = true;
            ShowCardContextMenu(item, element);
        }
    }

    private void ShowCardContextMenu(ClipboardItem item, FrameworkElement target)
    {
        var s = WindowsCM.Core.Localization.LocalizationManager.Strings;
        var menu = new ContextMenu();

        var pasteItem = new MenuItem { Header = s.ContextMenuPaste };
        pasteItem.Click += (_, _) =>
        {
            StartActivation(new ActivationRequest(item.Id, false), shiftHeld: false);
        };
        menu.Items.Add(pasteItem);

        var copyItem = new MenuItem { Header = s.ContextMenuCopy };
        copyItem.Click += (_, _) =>
        {
            StartActivation(new ActivationRequest(item.Id, false), shiftHeld: true);
        };
        menu.Items.Add(copyItem);

        var pinItem = new MenuItem { Header = item.Pinned ? s.ContextMenuUnpin : s.ContextMenuPin };
        pinItem.Click += (_, _) =>
        {
            TogglePinItem(item);
        };
        menu.Items.Add(pinItem);

        menu.Items.Add(new Separator());

        var applicable = _app.Executor.Applicable(_app.Actions, item);
        var commandActions = applicable.Where(a => a is not QrCodeAction && a.Id != BuiltinActions.QrCode && a is not ColorAction).ToList();
        var colorActions = applicable.OfType<ColorAction>().ToList();

        if (commandActions.Count > 0)
        {
            foreach (var action in commandActions)
            {
                var captured = action;
                var localized = BuiltinActions.GetLocalizedName(captured.Id, captured.Name);
                var actionEntry = new MenuItem { Header = localized };
                actionEntry.Click += (_, _) => _ = _app.RunActionAsync(captured, item);
                menu.Items.Add(actionEntry);
            }
        }

        if (colorActions.Count > 0)
        {
            var convertMenu = new MenuItem { Header = s.ContextMenuConvert };
            foreach (var colorAct in colorActions)
            {
                var captured = colorAct;
                var subHeader = captured.Name;
                if (subHeader.StartsWith("Converter para ", StringComparison.OrdinalIgnoreCase))
                {
                    subHeader = subHeader["Converter para ".Length..];
                }
                else if (subHeader.StartsWith("Convert to ", StringComparison.OrdinalIgnoreCase))
                {
                    subHeader = subHeader["Convert to ".Length..];
                }
                var colorEntry = new MenuItem { Header = subHeader.ToUpperInvariant() };
                colorEntry.Click += (_, _) => _ = _app.RunActionAsync(captured, item);
                convertMenu.Items.Add(colorEntry);
            }
            menu.Items.Add(convertMenu);
        }

        var qrItem = new MenuItem { Header = s.ContextMenuGenerateQr, InputGestureText = "Ctrl+Q" };
        qrItem.Click += (_, _) =>
        {
            _app.ShowQr(item);
        };
        menu.Items.Add(qrItem);

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem { Header = s.ContextMenuDelete };
        deleteItem.Click += (_, _) =>
        {
            DeleteItem(item);
        };
        menu.Items.Add(deleteItem);

        menu.PlacementTarget = target;
        menu.IsOpen = true;
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        Hide();
        _app.OpenSettings();
    }

    private void OnIncognitoClicked(object sender, RoutedEventArgs e)
    {
        _app.SetIncognito(!_app.IsIncognito);
        UpdateIncognitoIndicator();
    }

    private void OnClearClicked(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            WindowsCM.Core.Localization.LocalizationManager.Strings.CompactClearConfirm,
            "WindowsCM", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _app.Store.Clear(
                keepProtected: true,
                protectPinned: _app.Settings.Behavior.ProtectPinned,
                protectTagged: _app.Settings.Behavior.ProtectTagged);
            _model.Refresh();
            RefreshView();
        }
    }

    public void UpdateLanguage()
    {
        UpdateIncognitoIndicator();
        RefreshView();
    }

    private int? RowIndexAt(DependencyObject? source)
    {
        var item = VisualTreeWalk.FindAncestor<ListBoxItem>(source);
        if (item is null)
        {
            return null;
        }
        var index = ItemsList.ItemContainerGenerator.IndexFromContainer(item);
        return index >= 0 ? index : null;
    }

    private static T? FindDescendant<T>(DependencyObject? current) where T : DependencyObject
    {
        if (current is null)
        {
            return null;
        }
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(current);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(current, i);
            if (child is T match)
            {
                return match;
            }
            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }
        return null;
    }
}
