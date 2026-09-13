// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsCM.Core.Actions;
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Settings;
using WinForms = System.Windows.Forms;


namespace WindowsCM.App;

// Card-strip popup (prototype verdict A: Copyous density, Dark, full-width
// rows). Thin renderer over PopupViewModel: every gesture resolves through
// PopupKeyboardMap, every mutation goes through the model + store, and
// execution (paste/actions) belongs to App. Opens explicitly activated so
// keyboard-first operation works immediately — the ShowActivated=False
// default only stops the window stealing focus on its own.
public partial class PopupWindow : Window
{
    private readonly PopupViewModel _model;
    private readonly App _app;
    private bool _syncingSelection;
    private bool _isActivating;
    private long _lastShowTimestamp;
    private long _lastHideTimestamp;
    private bool _isLightTheme;
    private bool _isContextMenuOpen;
    private bool _isDialogOpen;
    private readonly SmoothScrollController _scrollController = new();
    private bool _isRenderingHooked;
    private long _lastRenderTicks;

    public bool WasRecentlyHidden => Environment.TickCount64 - _lastHideTimestamp < 350;
    private ColorScheme _currentScheme = ColorScheme.Dark;
    public ColorScheme CurrentScheme => _currentScheme;
    public bool IsLightTheme => _currentScheme == ColorScheme.Light;


    public PopupWindow(PopupViewModel model, App app)
    {
        _model = model;
        _app = app;
        InitializeComponent();
        ApplyTheme(ColorScheme.Dark);
        ItemsList.SelectionChanged += OnListSelectionChanged;
        FaviconService.FaviconUpdated += _ =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (IsVisible)
                {
                    ItemsList.Items.Refresh();
                }
            });
        };
    }

    public void ApplyTheme(ColorScheme scheme)
    {
        _currentScheme = scheme;
        _isLightTheme = scheme == ColorScheme.Light;
        var themeDict = PopupThemeBrushes.CreateThemeDictionary(scheme, _app.Settings.ItemColors);
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(themeDict);
        UpdateToggleButtons();
    }

    public void ApplyTheme(bool isLight) =>
        ApplyTheme(isLight ? ColorScheme.Light : ColorScheme.Dark);


    public void ShowAtCursor(bool incognito)
    {
        _isActivating = false;
        _lastShowTimestamp = Environment.TickCount64;
        SizeToContent = SizeToContent.Manual;
        ApplyLayoutOrientation(_app.Settings.Dialog.Orientation);
        if (!_app.Settings.Behavior.RememberSearch && !string.IsNullOrEmpty(_model.SearchText))
        {
            _model.SetSearch("");
        }
        _model.Show(incognito);
        SearchBox.Text = _model.SearchText;
        RefreshView();
        _app.EnsureLinkPreviewsForRecentItems();
        PlaceAtCursor(incognito);
        ResetScrollToInitial();
        UpdateLayout();
        Show();
        var handle = new WindowInteropHelper(this).EnsureHandle();
        SetForegroundWindow(handle);
        Activate();
        ItemsList.Focus();
    }

    public void ApplyLayoutOrientation(DialogOrientation orientation)
    {
        if (orientation == DialogOrientation.Vertical)
        {
            if (TryFindResource("VerticalItemsPanelTemplate") is ItemsPanelTemplate vPanel)
            {
                ItemsList.ItemsPanel = vPanel;
            }
            if (TryFindResource("VerticalCardItemStyle") is Style vStyle)
            {
                ItemsList.ItemContainerStyle = vStyle;
            }
            ScrollViewer.SetHorizontalScrollBarVisibility(ItemsList, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(ItemsList, ScrollBarVisibility.Auto);

            // Responsive TopBar: capsule on row 0 (spans all 3 cols), actions on row 1
            if (TopBarGrid != null && TopBarGrid.RowDefinitions.Count > 1)
            {
                TopBarGrid.RowDefinitions[1].Height = GridLength.Auto;
                if (SearchCapsuleBorder != null)
                {
                    Grid.SetRow(SearchCapsuleBorder, 0);
                    Grid.SetColumn(SearchCapsuleBorder, 0);
                    Grid.SetColumnSpan(SearchCapsuleBorder, 3);
                    SearchCapsuleBorder.Width = double.NaN;
                    SearchCapsuleBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                    SearchCapsuleBorder.Margin = new Thickness(0, 0, 0, 8);
                }
                if (LeftActionsPanel != null)
                {
                    Grid.SetRow(LeftActionsPanel, 1);
                    Grid.SetColumn(LeftActionsPanel, 0);
                }
                if (RightActionsPanel != null)
                {
                    Grid.SetRow(RightActionsPanel, 1);
                    Grid.SetColumn(RightActionsPanel, 2);
                }
            }
        }
        else
        {
            if (TryFindResource("HorizontalItemsPanelTemplate") is ItemsPanelTemplate hPanel)
            {
                ItemsList.ItemsPanel = hPanel;
            }
            if (TryFindResource("HorizontalCardItemStyle") is Style hStyle)
            {
                ItemsList.ItemContainerStyle = hStyle;
            }
            ScrollViewer.SetHorizontalScrollBarVisibility(ItemsList, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(ItemsList, ScrollBarVisibility.Disabled);

            // Responsive TopBar: Left actions, capsule (420px), Right actions all on row 0
            if (TopBarGrid != null && TopBarGrid.RowDefinitions.Count > 1)
            {
                TopBarGrid.RowDefinitions[1].Height = new GridLength(0);
                if (SearchCapsuleBorder != null)
                {
                    Grid.SetRow(SearchCapsuleBorder, 0);
                    Grid.SetColumn(SearchCapsuleBorder, 1);
                    Grid.SetColumnSpan(SearchCapsuleBorder, 1);
                    SearchCapsuleBorder.Width = 420;
                    SearchCapsuleBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                    SearchCapsuleBorder.Margin = new Thickness(0);
                }
                if (LeftActionsPanel != null)
                {
                    Grid.SetRow(LeftActionsPanel, 0);
                    Grid.SetColumn(LeftActionsPanel, 0);
                }
                if (RightActionsPanel != null)
                {
                    Grid.SetRow(RightActionsPanel, 0);
                    Grid.SetColumn(RightActionsPanel, 2);
                }
            }
        }
    }

    public new void Hide()
    {
        _isActivating = false;
        _lastHideTimestamp = Environment.TickCount64;
        ResetScrollToInitial();
        base.Hide();
        if (!_app.Settings.Behavior.RememberSearch)
        {
            _model.SetSearch("");
            SearchBox.Text = "";
        }

        ApplyScrollbarPosition();
    }

    public void ResetScrollToInitial()
    {
        StopSmoothScroll();
        var scrollViewer = FindVisualChild<ScrollViewer>(ItemsList);
        if (scrollViewer != null)
        {
            var orientation = _app.Settings.Dialog.Orientation;
            var recentAtStart = orientation == DialogOrientation.Vertical
                ? _app.Settings.Dialog.LargeVerticalOrder == VerticalItemOrder.RecentOnTop
                : _app.Settings.Dialog.LargeHorizontalOrder == HorizontalItemOrder.RecentOnLeft;

            if (recentAtStart)
            {
                scrollViewer.ScrollToHorizontalOffset(0);
                scrollViewer.ScrollToVerticalOffset(0);
                _scrollController.SetImmediate(0, scrollViewer.ScrollableWidth);
            }
            else
            {
                if (orientation == DialogOrientation.Horizontal)
                {
                    scrollViewer.ScrollToRightEnd();
                    _scrollController.SetImmediate(scrollViewer.ScrollableWidth, scrollViewer.ScrollableWidth);
                }
                else
                {
                    scrollViewer.ScrollToBottom();
                }
            }
        }
        else
        {
            _scrollController.SetImmediate(0, 0);
        }
    }

    public void ApplyScrollbarPosition()
    {
        ScrollbarPositionHelper.ApplyPositions(
            ItemsList,
            _app.Settings.Dialog.VerticalScrollbarPosition,
            _app.Settings.Dialog.HorizontalScrollbarPosition);
    }

    public void RefreshView()
    {
        var orientation = _app.Settings.Dialog.Orientation;
        var recentAtStart = orientation == DialogOrientation.Vertical
            ? _app.Settings.Dialog.LargeVerticalOrder == VerticalItemOrder.RecentOnTop
            : _app.Settings.Dialog.LargeHorizontalOrder == HorizontalItemOrder.RecentOnLeft;
        _model.SetItemOrdering(recentAtStart);

        ItemsList.ItemsSource = null;
        ItemsList.ItemsSource = _model.VisibleItems;

        if (_model.IsViewingIncognito && _model.VisibleItems.Count == 0)
        {
            if (IncognitoEmptyState != null) IncognitoEmptyState.Visibility = Visibility.Visible;
            ItemsList.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (IncognitoEmptyState != null) IncognitoEmptyState.Visibility = Visibility.Collapsed;
            ItemsList.Visibility = Visibility.Visible;
        }

        SyncSelectionFromModel();
        UpdateToggleButtons();
        ApplyScrollbarPosition();
    }

    private void UpdateToggleButtons()
    {
        if (_model.PinsOnly)
        {
            PinsButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
            PinsButton.Foreground = System.Windows.Media.Brushes.White;
        }
        else
        {
            PinsButton.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
            PinsButton.ClearValue(System.Windows.Controls.Button.ForegroundProperty);
        }

        var isIncognito = _model.IsIncognito;
        var s = WindowsCM.Core.Localization.LocalizationManager.Strings;
        if (isIncognito)
        {
            IncognitoButton.Background = TryFindResource("IncognitoActiveButtonBrush") as System.Windows.Media.Brush
                ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED));
            IncognitoButton.Foreground = System.Windows.Media.Brushes.White;
            IncognitoButton.BorderBrush = TryFindResource("IncognitoActiveButtonBorderBrush") as System.Windows.Media.Brush
                ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x93, 0x33, 0xEA));
            IncognitoButton.ToolTip = s.PopupIncognitoTooltipActive;

            if (IncognitoBanner != null) IncognitoBanner.Visibility = Visibility.Visible;
            if (IncognitoTopAccent != null) IncognitoTopAccent.Visibility = Visibility.Visible;

            if (_model.IsViewingIncognito)
            {
                if (IncognitoBannerTitle != null) IncognitoBannerTitle.Text = s.PopupIncognitoBannerTitle;
                if (IncognitoBannerSubtitle != null) IncognitoBannerSubtitle.Text = s.PopupIncognitoBannerSubtitle;
                if (IncognitoBannerDescription != null) IncognitoBannerDescription.Text = s.PopupIncognitoBannerDesc;
                if (SwitchHistoryText != null) SwitchHistoryText.Text = s.PopupSwitchHistoryNormal;
                if (SwitchHistoryIcon != null)
                {
                    SwitchHistoryIcon.Visibility = Visibility.Visible;
                    SwitchHistoryIcon.Text = "\uE81C";
                }
                if (SwitchHistoryIncognitoIcon != null)
                {
                    SwitchHistoryIncognitoIcon.Visibility = Visibility.Collapsed;
                }
                if (WindowRootBorder != null && TryFindResource("IncognitoBorderGlowBrush") is System.Windows.Media.Brush glow)
                {
                    WindowRootBorder.BorderBrush = glow;
                }
            }
            else
            {
                if (IncognitoBannerTitle != null) IncognitoBannerTitle.Text = s.PopupIncognitoBackgroundTitle;
                if (IncognitoBannerSubtitle != null) IncognitoBannerSubtitle.Text = s.PopupIncognitoBackgroundSubtitle;
                if (IncognitoBannerDescription != null) IncognitoBannerDescription.Text = s.PopupIncognitoBackgroundDesc;
                if (SwitchHistoryText != null) SwitchHistoryText.Text = s.PopupSwitchHistoryIncognito;
                if (SwitchHistoryIcon != null)
                {
                    SwitchHistoryIcon.Visibility = Visibility.Collapsed;
                }
                if (SwitchHistoryIncognitoIcon != null)
                {
                    SwitchHistoryIncognitoIcon.Visibility = Visibility.Visible;
                }
                if (WindowRootBorder != null && TryFindResource("PopupBorderBrush") is System.Windows.Media.Brush normalBorder)
                {
                    WindowRootBorder.BorderBrush = normalBorder;
                }
            }
        }
        else
        {
            IncognitoButton.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
            IncognitoButton.ClearValue(System.Windows.Controls.Button.ForegroundProperty);
            IncognitoButton.ClearValue(System.Windows.Controls.Button.BorderBrushProperty);
            IncognitoButton.ToolTip = s.PopupIncognitoTooltipInactive;

            if (IncognitoBanner != null) IncognitoBanner.Visibility = Visibility.Collapsed;
            if (IncognitoTopAccent != null) IncognitoTopAccent.Visibility = Visibility.Collapsed;
            if (WindowRootBorder != null && TryFindResource("PopupBorderBrush") is System.Windows.Media.Brush normalBorder)
            {
                WindowRootBorder.BorderBrush = normalBorder;
            }
        }


        UpdateSearchPlaceholder();
        UpdateSearchFilterVisuals();
    }

    private void UpdateSearchPlaceholder()
    {
        if (SearchPlaceholder != null)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void UpdateSearchFilterVisuals()
    {
        if (SearchFilterIcon == null) return;
        var s = WindowsCM.Core.Localization.LocalizationManager.Strings;

        if (_model.TypeFilter.HasValue)
        {
            var kind = _model.TypeFilter.Value;
            var kindName = kind switch
            {
                ItemKind.Link => s.FilterLinks,
                ItemKind.Code => s.FilterCode,
                ItemKind.File => s.FilterFiles,
                ItemKind.Image => s.FilterImages,
                ItemKind.Character => s.FilterEmojis,
                ItemKind.Color => s.FilterColors,
                _ => s.FilterText
            };
            SearchFilterButton.ToolTip = string.Format(s.PopupFilterActiveTooltip, kindName);
            SearchFilterIcon.Text = ItemDisplayFormatter.GetKindIconGlyph(kind);
            var brushKey = kind switch
            {
                ItemKind.Link => "KindLinkBrush",
                ItemKind.Code => "KindCodeBrush",
                ItemKind.File => "KindFileBrush",
                ItemKind.Image => "KindImageBrush",
                ItemKind.Character => "KindCharBrush",
                ItemKind.Color => "KindColorBrush",
                _ => "KindTextBrush"
            };
            if (TryFindResource(brushKey) is System.Windows.Media.Brush b)
            {
                SearchFilterIcon.Foreground = b;
            }
        }
        else
        {
            SearchFilterButton.ToolTip = s.PopupFilterTooltip;
            SearchFilterIcon.Text = "\uE721";
            SearchFilterIcon.SetResourceReference(TextBlock.ForegroundProperty, "SearchIconBrush");
        }
    }

    private void OnSearchFilterClicked(object sender, RoutedEventArgs e)
    {
        var s = WindowsCM.Core.Localization.LocalizationManager.Strings;
        var menu = new ContextMenu();
        _isContextMenuOpen = true;

        menu.Closed += (_, _) =>
        {
            _isContextMenuOpen = false;
            if (!IsActive && !_isActivating && !_isDialogOpen)
            {
                Hide();
            }
        };

        var iconFont = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");

        MenuItem CreateItem(string label, string glyph, ItemKind? kind)
        {
            var isSelected = _model.TypeFilter == kind;
            var item = new MenuItem
            {
                Header = isSelected ? $"✓  {label}" : $"     {label}",
                Icon = new TextBlock
                {
                    Text = glyph,
                    FontFamily = iconFont,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                },
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            item.Click += (_, _) =>
            {
                _model.SetTypeFilter(kind);
                RefreshView();
            };
            return item;
        }

        menu.Items.Add(CreateItem(s.FilterAll, "\uE71D", null));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateItem(s.FilterLinks, "\uE71B", ItemKind.Link));
        menu.Items.Add(CreateItem(s.FilterCode, "\uE943", ItemKind.Code));
        menu.Items.Add(CreateItem(s.FilterFiles, "\uED43", ItemKind.File));
        menu.Items.Add(CreateItem(s.FilterImages, "\uEB9F", ItemKind.Image));
        menu.Items.Add(CreateItem(s.FilterEmojis, "\uED53", ItemKind.Character));
        menu.Items.Add(CreateItem(s.FilterColors, "\uE790", ItemKind.Color));
        menu.Items.Add(CreateItem(s.FilterText, "\uE8A5", ItemKind.Text));

        menu.PlacementTarget = SearchFilterButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void PlaceAtCursor(bool incognito)
    {
        // First-open parity: before the first Show there is no
        // PresentationSource, so force the HWND and read the real DPI
        // transform — otherwise the first open measures with Identity while
        // later opens use the monitor scale.
        var handle = new WindowInteropHelper(this).EnsureHandle();
        var transform = HwndSource.FromHwnd(handle)?.CompositionTarget?.TransformFromDevice
            ?? PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        GetCursorPos(out var cursor);
        var screen = WinForms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        var area = screen.WorkingArea;
        var topLeft = transform.Transform(new System.Windows.Point(area.Left, area.Top));
        var bottomRight = transform.Transform(new System.Windows.Point(area.Right, area.Bottom));
        var workArea = new WorkArea(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);

        var orientation = _app.Settings.Dialog.Orientation;
        var hPos = _app.Settings.Dialog.LargeHorizontalPosition;
        var vPos = _app.Settings.Dialog.LargeVerticalPosition;

        var (left, top, width, height) = PopupPlacement.PlaceLargePopup(
            orientation, hPos, vPos, workArea);
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateSearchPlaceholder();
        UpdateSearchFilterVisuals();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        var elapsed = Environment.TickCount64 - _lastShowTimestamp;
        if (!PopupDeactivationPolicy.ShouldHide(IsVisible, elapsed, _isContextMenuOpen, _isDialogOpen))
        {
            return;
        }
        Hide();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsVisible)
        {
            return;
        }
        UpdateSearchPlaceholder();
        // By design this never repositions or resizes: SizeToContent is
        // Manual while open (ticket 21), so filtering only swaps rows.
        _model.SetSearch(SearchBox.Text);
        RefreshView();
    }

    private void OnPinsClicked(object sender, RoutedEventArgs e)
    {
        _model.TogglePinsFilter();
        RefreshView();
    }

    private void OnIncognitoClicked(object sender, RoutedEventArgs e) =>
        _app.SetIncognito(!_model.IsIncognito);

    private void OnExitIncognitoClicked(object sender, RoutedEventArgs e) =>
        _app.SetIncognito(false);

    private void OnSwitchHistoryClicked(object sender, RoutedEventArgs e)
    {
        if (_model.IsViewingIncognito)
        {
            _model.SwitchViewToPersistent();
        }
        else
        {
            _model.SwitchViewToIncognito();
        }
        RefreshView();
    }


    private void OnClearClicked(object sender, RoutedEventArgs e)
    {
        _model.ClearKeepProtected();
        RefreshView();
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        Hide();
        _app.OpenSettings();
    }

    private void OnItemsListPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_app.Settings.Dialog.Orientation == DialogOrientation.Vertical)
        {
            return;
        }
        if (ItemsList == null) return;
        var scrollViewer = FindVisualChild<ScrollViewer>(ItemsList);
        if (scrollViewer != null)
        {
            if (!_scrollController.IsAnimating)
            {
                _scrollController.SetImmediate(scrollViewer.HorizontalOffset, scrollViewer.ScrollableWidth);
            }

            // 1 card width (250) + margin (10) = 260 DIPs per wheel notch
            var deltaCards = e.Delta / 120.0;
            _scrollController.AddDelta(-deltaCards * 260.0, scrollViewer.ScrollableWidth);

            StartSmoothScroll();
            e.Handled = true;
        }
    }

    private void StartSmoothScroll()
    {
        if (!_isRenderingHooked)
        {
            _lastRenderTicks = Stopwatch.GetTimestamp();
            CompositionTarget.Rendering += OnRenderFrame;
            _isRenderingHooked = true;
        }
    }

    private void StopSmoothScroll()
    {
        if (_isRenderingHooked)
        {
            CompositionTarget.Rendering -= OnRenderFrame;
            _isRenderingHooked = false;
        }
    }

    private void OnRenderFrame(object? sender, EventArgs e)
    {
        if (ItemsList == null)
        {
            StopSmoothScroll();
            return;
        }

        var scrollViewer = FindVisualChild<ScrollViewer>(ItemsList);
        if (scrollViewer == null)
        {
            StopSmoothScroll();
            return;
        }

        var nowTicks = Stopwatch.GetTimestamp();
        var dt = (double)(nowTicks - _lastRenderTicks) / Stopwatch.Frequency;
        _lastRenderTicks = nowTicks;

        if (dt > 0.05) dt = 0.05;

        var keepAnimating = _scrollController.Tick(dt);
        scrollViewer.ScrollToHorizontalOffset(_scrollController.CurrentOffset);

        if (!keepAnimating)
        {
            StopSmoothScroll();
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                return typed;
            }
            var found = FindVisualChild<T>(child);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private void OnItemDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        // Ticket 23 idempotence: single-click (PreviewMouseLeftButtonUp)
        // already handled activation and set _isActivating; double-click is
        // an idempotent no-op so it neither duplicates nor breaks.
    }

    // Ticket 23: single click copies and pastes like Enter; Shift+click
    // copies only (the orchestrator turns shiftHeld into CopyOnly: no
    // injection, no hide, diagnostics still surfaced). MouseUp — not
    // MouseDown — so press-drag-release off the row or onto the scrollbar
    // never activates. Keyboard navigation (arrows/Home/End/slots) only
    // changes SelectedIndex via OnListSelectionChanged and never reaches
    // here, so it never pastes on its own.
    private void OnItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (!IsVisible || _isActivating)
        {
            return;
        }
        var isInteractive = FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(e.OriginalSource as DependencyObject) is not null;
        var index = RowIndexAt(e.OriginalSource as DependencyObject);
        if (!PopupClickPolicy.ShouldActivate(IsVisible, index, isInteractive))
        {
            return;
        }
        var shiftHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var request = _model.ActivateAt(index!.Value, runDefaultAction: false);
        if (request is not null)
        {
            if (!shiftHeld)
            {
                _isActivating = true;
            }
            _ = _app.ActivateAsync(request, shiftHeld);
        }
    }

    // Row under the mouse-up point, or null for empty area / scrollbar /
    // header (OriginalSource outside any ListBoxItem). IndexFromContainer
    // returns -1 when the container is unrealized; normalize to null so the
    // click policy sees a single "no row" shape.
    private int? RowIndexAt(DependencyObject? source)
    {
        var item = FindAncestor<ListBoxItem>(source);
        if (item is null)
        {
            return null;
        }
        var index = ItemsList.ItemContainerGenerator.IndexFromContainer(item);
        return index >= 0 ? index : null;
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Ctrl+Q (QR) never reaches the keymap — PopupKey has no Q chord —
        // so the shell owns it directly (QrActions eligibility + dialog).
        if (e.Key == Key.Q
            && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)
            && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            ShowQrForSelected();
            return;
        }
        var mapped = MapKey(e);
        if (mapped is null)
        {
            return;
        }
        var result = PopupKeyboardMap.Resolve(mapped);
        if (result is null)
        {
            return;
        }
        e.Handled = true;
        ApplyAction(result);
    }

    private PopupKeyEvent? MapKey(System.Windows.Input.KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            return null;
        }
        var key = e.Key switch
        {
            Key.Enter => (PopupKey?)PopupKey.Enter,
            Key.Space => PopupKey.Space,
            Key.Delete => PopupKey.Delete,
            Key.Up => PopupKey.Up,
            Key.Down => PopupKey.Down,
            Key.Left => PopupKey.Left,
            Key.Right => PopupKey.Right,
            Key.Tab => PopupKey.Tab,
            Key.Home => PopupKey.Home,
            Key.End => PopupKey.End,
            Key.Escape => PopupKey.Escape,
            Key.A => PopupKey.A,
            Key.E => PopupKey.E,
            Key.F => PopupKey.F,
            Key.P => PopupKey.P,
            Key.S => PopupKey.S,
            Key.T => PopupKey.T,
            Key.D0 => PopupKey.D0,
            Key.D1 => PopupKey.D1,
            Key.D2 => PopupKey.D2,
            Key.D3 => PopupKey.D3,
            Key.D4 => PopupKey.D4,
            Key.D5 => PopupKey.D5,
            Key.D6 => PopupKey.D6,
            Key.D7 => PopupKey.D7,
            Key.D8 => PopupKey.D8,
            Key.D9 => PopupKey.D9,
            Key.NumPad0 => PopupKey.NumPad0,
            Key.NumPad1 => PopupKey.NumPad1,
            Key.NumPad2 => PopupKey.NumPad2,
            Key.NumPad3 => PopupKey.NumPad3,
            Key.NumPad4 => PopupKey.NumPad4,
            Key.NumPad5 => PopupKey.NumPad5,
            Key.NumPad6 => PopupKey.NumPad6,
            Key.NumPad7 => PopupKey.NumPad7,
            Key.NumPad8 => PopupKey.NumPad8,
            Key.NumPad9 => PopupKey.NumPad9,
            Key.Oem3 => PopupKey.Oem3,
            _ => null,
        };
        if (key is null)
        {
            return null;
        }
        var modifiers = PopupModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            modifiers |= PopupModifiers.Ctrl;
        }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= PopupModifiers.Shift;
        }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= PopupModifiers.Alt;
        }
        return new PopupKeyEvent(key.Value, modifiers, SearchBox.IsFocused);
    }

    private void ApplyAction(PopupKeyResult result)
    {
        switch (result.Action)
        {
            case PopupAction.MoveNext: _model.MoveNext(); SyncSelectionFromModel(); break;
            case PopupAction.MovePrevious: _model.MovePrevious(); SyncSelectionFromModel(); break;
            case PopupAction.MoveFirst: _model.MoveFirst(); SyncSelectionFromModel(); break;
            case PopupAction.MoveLast: _model.MoveLast(); SyncSelectionFromModel(); break;
            case PopupAction.JumpToSlot: _model.JumpToSlot(result.Slot); SyncSelectionFromModel(); break;
            case PopupAction.TogglePinsFilter: _model.TogglePinsFilter(); RefreshView(); break;
            case PopupAction.CycleTypeNext: _model.CycleTypeNext(); RefreshView(); break;
            case PopupAction.CycleTypePrevious: _model.CycleTypePrevious(); RefreshView(); break;
            case PopupAction.CycleTagNext: _model.CycleTagNext(); RefreshView(); break;
            case PopupAction.CycleTagPrevious: _model.CycleTagPrevious(); RefreshView(); break;
            case PopupAction.Delete: _model.DeleteSelected(force: false); RefreshView(); break;
            case PopupAction.DeleteForce: _model.DeleteSelected(force: true); RefreshView(); break;
            case PopupAction.TogglePin: _model.TogglePinSelected(); RefreshView(); break;
            case PopupAction.ActivateSelected:
                ActivateCurrent(runDefaultAction: false, result.ShiftHeld);
                break;
            case PopupAction.ActivateDefault:
                ActivateCurrent(runDefaultAction: true, shiftHeld: false);
                break;
            case PopupAction.EditItem: EditCurrentTitle(isTitle: false); break;
            case PopupAction.EditTitle: EditCurrentTitle(isTitle: true); break;
            case PopupAction.ShowActionsMenu: ShowActionsMenu(); break;
            case PopupAction.FocusSearch: SearchBox.Focus(); SearchBox.SelectAll(); break;
            case PopupAction.ApplyTagSlot: ApplyTagSlot(result.Slot); break;
            case PopupAction.Close: Hide(); break;
        }
    }

    private void ActivateCurrent(bool runDefaultAction, bool shiftHeld)
    {
        var request = _model.ActivateSelected(runDefaultAction);
        if (request is not null)
        {
            _ = _app.ActivateAsync(request, shiftHeld);
        }
    }

    private void OnReceiveMobileClicked(object sender, RoutedEventArgs e)
    {
        _isDialogOpen = true;
        try
        {
            _app.ShowMobileTransfer();
        }
        finally
        {
            _isDialogOpen = false;
        }
    }

    private void ShowQrForSelected()
    {
        if (_model.SelectedItem is not { } item)
        {
            return;
        }
        _isDialogOpen = true;
        try
        {
            _app.ShowQr(item);
        }
        finally
        {
            _isDialogOpen = false;
        }
    }

    private void ApplyTagSlot(int slot)
    {
        if (_model.SelectedItem is { } item)
        {
            _app.ApplyTagSlot(item, slot);
        }
    }

    private void EditCurrentTitle(bool isTitle)
    {
        if (_model.SelectedItem is not { } item)
        {
            return;
        }
        _isDialogOpen = true;
        try
        {
            var s = WindowsCM.Core.Localization.LocalizationManager.Strings;
            var text = TextInputDialog.Prompt(
                isTitle ? s.DialogEditTitle : s.DialogEditContent,
                isTitle ? item.Title ?? "" : item.Content,
                multiline: !isTitle);
            if (text is null)
            {
                return;
            }
            if (isTitle)
            {
                _app.Store.SetTitle(item.Id, string.IsNullOrWhiteSpace(text) ? null : text.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                _app.Store.TryUpdateContent(item.Id, item.Kind, text);
            }
            _model.Refresh();
            RefreshView();
        }
        finally
        {
            _isDialogOpen = false;
            if (!IsActive && !_isActivating)
            {
                Hide();
            }
        }
    }

    private void OnCardPinButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardItem item })
        {
            _app.Store.SetPinned(item.Id, !item.Pinned);
            _model.Refresh();
            RefreshView();
            e.Handled = true;
        }
    }

    private void OnCardQrButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardItem item })
        {
            _isDialogOpen = true;
            try
            {
                _app.ShowQr(item);
            }
            finally
            {
                _isDialogOpen = false;
            }
            e.Handled = true;
        }
    }

    private void OnCardDeleteButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardItem item })
        {
            _app.Store.Delete(item.Id);
            _model.Refresh();
            RefreshView();
            e.Handled = true;
        }
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
            ShowCardContextMenu(item, element);
            e.Handled = true;
        }
    }

    private void ShowActionsMenu()
    {
        if (_model.SelectedItem is { } item)
        {
            ShowCardContextMenu(item, ItemsList);
        }
    }

    private void ShowCardContextMenu(ClipboardItem item, FrameworkElement target)
    {
        var s = WindowsCM.Core.Localization.LocalizationManager.Strings;
        var menu = new ContextMenu();
        _isContextMenuOpen = true;

        menu.Closed += (_, _) =>
        {
            _isContextMenuOpen = false;
            if (!IsActive && !_isActivating && !_isDialogOpen)
            {
                Hide();
            }
        };

        var pasteItem = new MenuItem { Header = s.ContextMenuPaste, InputGestureText = "Enter" };
        pasteItem.Click += (_, _) =>
        {
            var req = new ActivationRequest(item.Id, RunDefaultAction: false);
            _isActivating = true;
            _ = _app.ActivateAsync(req, shiftHeld: false);
        };
        menu.Items.Add(pasteItem);

        var copyItem = new MenuItem { Header = s.ContextMenuCopy, InputGestureText = "Shift+Enter" };
        copyItem.Click += (_, _) =>
        {
            var req = new ActivationRequest(item.Id, RunDefaultAction: false);
            _ = _app.ActivateAsync(req, shiftHeld: true);
        };
        menu.Items.Add(copyItem);

        var pinItem = new MenuItem
        {
            Header = item.Pinned ? s.ContextMenuUnpin : s.ContextMenuPin,
            InputGestureText = "Alt+P"
        };
        pinItem.Click += (_, _) =>
        {
            _app.Store.SetPinned(item.Id, !item.Pinned);
            _model.Refresh();
            RefreshView();
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
            _isDialogOpen = true;
            try
            {
                _app.ShowQr(item);
            }
            finally
            {
                _isDialogOpen = false;
            }
        };
        menu.Items.Add(qrItem);

        menu.Items.Add(new Separator());

        var editTitleItem = new MenuItem { Header = s.ContextMenuEditTitle, InputGestureText = "F2" };
        editTitleItem.Click += (_, _) =>
        {
            _isDialogOpen = true;
            try
            {
                var text = TextInputDialog.Prompt(s.DialogEditTitle, item.Title ?? "", multiline: false);
                if (text is not null)
                {
                    _app.Store.SetTitle(item.Id, string.IsNullOrWhiteSpace(text) ? null : text.Trim());
                    _model.Refresh();
                    RefreshView();
                }
            }
            finally
            {
                _isDialogOpen = false;
                if (!IsActive && !_isActivating)
                {
                    Hide();
                }
            }
        };
        menu.Items.Add(editTitleItem);

        var editContentItem = new MenuItem { Header = s.ContextMenuEditContent, InputGestureText = "Ctrl+E" };
        editContentItem.Click += (_, _) =>
        {
            _isDialogOpen = true;
            try
            {
                var text = TextInputDialog.Prompt(s.DialogEditContent, item.Content, multiline: true);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _app.Store.TryUpdateContent(item.Id, item.Kind, text);
                    _model.Refresh();
                    RefreshView();
                }
            }
            finally
            {
                _isDialogOpen = false;
                if (!IsActive && !_isActivating)
                {
                    Hide();
                }
            }
        };
        menu.Items.Add(editContentItem);

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem { Header = s.ContextMenuDelete, InputGestureText = "Delete" };
        deleteItem.Click += (_, _) =>
        {
            _app.Store.Delete(item.Id);
            _model.Refresh();
            RefreshView();
        };
        menu.Items.Add(deleteItem);

        menu.PlacementTarget = target;
        if (target is System.Windows.Controls.Button)
        {
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        }
        else
        {
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        }
        menu.IsOpen = true;
    }

    private void SyncSelectionFromModel()
    {
        if (_syncingSelection)
        {
            return;
        }
        _syncingSelection = true;
        try
        {
            ItemsList.SelectedIndex = _model.SelectedIndex;
            if (_model.SelectedItem is not null)
            {
                ItemsList.ScrollIntoView(_model.SelectedItem);
            }
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || ItemsList.SelectedIndex < 0)
        {
            return;
        }
        // The model owns selection: the view only reports the clicked row.
        _model.SetSelectedIndex(ItemsList.SelectedIndex);
        SyncSelectionFromModel();
    }

    public void UpdateLanguage()
    {
        UpdateToggleButtons();
        RefreshView();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out System.Drawing.Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
