// SPDX-License-Identifier: GPL-3.0-or-later
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

    public PopupWindow(PopupViewModel model, App app)
    {
        _model = model;
        _app = app;
        InitializeComponent();
        ItemsList.SelectionChanged += OnListSelectionChanged;
    }

    public void ShowAtCursor(bool incognito)
    {
        _isActivating = false;
        SizeToContent = SizeToContent.Manual;
        _model.Show(incognito);
        if (!_app.Settings.Behavior.RememberSearch)
        {
            _model.SetSearch("");
        }
        SearchBox.Text = _model.SearchText;
        RefreshView();
        // Show before measuring: a never-shown Window lays out to zero, so
        // the first open must share the post-layout path with later opens.
        // The window stays transparent until placed, so there is no flash
        // at a stale position (first open == later opens, no jumps).
        Opacity = 0;
        try
        {
            Show();
            UpdateLayout();
            PlaceAtCursor(incognito);
        }
        finally
        {
            Opacity = 1;
        }
        Activate();
        ItemsList.Focus();
    }

    public new void Hide()
    {
        _isActivating = false;
        base.Hide();
        if (!_app.Settings.Behavior.RememberSearch)
        {
            _model.SetSearch("");
            SearchBox.Text = "";
        }
    }

    public void RefreshView()
    {
        ItemsList.ItemsSource = null;
        ItemsList.ItemsSource = _model.VisibleItems;
        SyncSelectionFromModel();
        UpdateToggleButtons();
    }

    private void UpdateToggleButtons()
    {
        if (_model.PinsOnly)
        {
            PinsButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
            PinsButton.Foreground = System.Windows.Media.Brushes.White;
            PinsButton.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x90, 0xFF));
        }
        else
        {
            PinsButton.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
            PinsButton.ClearValue(System.Windows.Controls.Button.ForegroundProperty);
            PinsButton.ClearValue(System.Windows.Controls.Button.BorderBrushProperty);
        }

        if (_model.IsIncognito)
        {
            IncognitoButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
            IncognitoButton.Foreground = System.Windows.Media.Brushes.White;
            IncognitoButton.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x90, 0xFF));
        }
        else
        {
            IncognitoButton.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
            IncognitoButton.ClearValue(System.Windows.Controls.Button.ForegroundProperty);
            IncognitoButton.ClearValue(System.Windows.Controls.Button.BorderBrushProperty);
        }
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
        var cursorDips = transform.Transform(new System.Windows.Point(cursor.X, cursor.Y));
        var topLeft = transform.Transform(new System.Windows.Point(area.Left, area.Top));
        var bottomRight = transform.Transform(new System.Windows.Point(area.Right, area.Bottom));
        var placedHeight = PopupSizing.ClampHeight(ActualHeight > 0 ? ActualHeight : PopupSizing.MaxHeight);
        var workArea = new WorkArea(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        var (left, top, width) = PopupPlacement.PlaceHorizontalFill(
            cursorDips.Y, placedHeight, workArea);
        Left = left;
        Top = top;
        Width = width;
        Height = placedHeight;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
    }

    private void OnDeactivated(object? sender, EventArgs e) => Hide();

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsVisible)
        {
            return;
        }
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

    private void OnClearClicked(object sender, RoutedEventArgs e)
    {
        _model.ClearKeepProtected();
        RefreshView();
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e) => _app.OpenSettings();

    public static readonly DependencyProperty AnimatedOffsetProperty =
        DependencyProperty.RegisterAttached(
            "AnimatedOffset",
            typeof(double),
            typeof(PopupWindow),
            new FrameworkPropertyMetadata(0.0, OnAnimatedOffsetChanged));

    private static void OnAnimatedOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv)
        {
            sv.ScrollToHorizontalOffset((double)e.NewValue);
        }
    }

    private double _targetHorizontalOffset;
    private bool _isScrollingAnimated;

    private void OnItemsListPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ItemsList == null) return;
        var scrollViewer = FindVisualChild<ScrollViewer>(ItemsList);
        if (scrollViewer != null)
        {
            if (!_isScrollingAnimated)
            {
                _targetHorizontalOffset = scrollViewer.HorizontalOffset;
            }

            // 1 card width (250) + margin (10) = 260 DIPs per wheel notch
            var deltaCards = e.Delta / 120.0;
            _targetHorizontalOffset = Math.Clamp(
                _targetHorizontalOffset - (deltaCards * 260.0),
                0,
                scrollViewer.ScrollableWidth);

            var anim = new DoubleAnimation
            {
                From = scrollViewer.HorizontalOffset,
                To = _targetHorizontalOffset,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            anim.Completed += (_, _) => _isScrollingAnimated = false;
            _isScrollingAnimated = true;
            scrollViewer.BeginAnimation(AnimatedOffsetProperty, anim);
            e.Handled = true;
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
        var index = RowIndexAt(e.OriginalSource as DependencyObject);
        if (!PopupClickPolicy.ShouldActivate(IsVisible, index))
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

    private void ShowQrForSelected()
    {
        if (_model.SelectedItem is not { } item)
        {
            return;
        }
        var payload = QrActions.Payload(item.Kind, item.Content);
        if (payload is null)
        {
            return;
        }
        _app.ShowQr(payload);
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
        var text = TextInputDialog.Prompt(
            isTitle ? "Editar título" : "Editar conteúdo",
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
        var menu = new ContextMenu();

        var pasteItem = new MenuItem { Header = "Colar", InputGestureText = "Enter" };
        pasteItem.Click += (_, _) =>
        {
            var req = new ActivationRequest(item.Id, RunDefaultAction: false);
            _isActivating = true;
            _ = _app.ActivateAsync(req, shiftHeld: false);
        };
        menu.Items.Add(pasteItem);

        var copyItem = new MenuItem { Header = "Copiar", InputGestureText = "Shift+Enter" };
        copyItem.Click += (_, _) =>
        {
            var req = new ActivationRequest(item.Id, RunDefaultAction: false);
            _ = _app.ActivateAsync(req, shiftHeld: true);
        };
        menu.Items.Add(copyItem);

        var pinItem = new MenuItem
        {
            Header = item.Pinned ? "Desafixar" : "Fixar",
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
        if (applicable.Count > 0)
        {
            var actionsSubmenu = new MenuItem { Header = "Ações" };
            foreach (var action in applicable)
            {
                var captured = action;
                var actionEntry = new MenuItem { Header = captured.Name };
                actionEntry.Click += (_, _) => _ = _app.RunActionAsync(captured, item);
                actionsSubmenu.Items.Add(actionEntry);
            }
            menu.Items.Add(actionsSubmenu);
        }

        var qrPayload = QrActions.Payload(item.Kind, item.Content);
        if (qrPayload is not null)
        {
            var qrItem = new MenuItem { Header = "Gerar código QR", InputGestureText = "Ctrl+Q" };
            qrItem.Click += (_, _) => _app.ShowQr(qrPayload);
            menu.Items.Add(qrItem);
        }

        var tagsSubmenu = new MenuItem { Header = "Tags" };
        var noneTag = new MenuItem { Header = "Nenhuma tag" };
        noneTag.Click += (_, _) =>
        {
            _app.Store.SetTag(item.Id, null);
            _model.Refresh();
            RefreshView();
        };
        tagsSubmenu.Items.Add(noneTag);

        for (int i = 0; i < ItemTags.All.Count; i++)
        {
            var tagHex = ItemTags.All[i];
            var tagSlot = i + 1;
            var tagItem = new MenuItem
            {
                Header = $"Tag {tagSlot} ({tagHex})",
                IsChecked = string.Equals(item.Tag, tagHex, StringComparison.OrdinalIgnoreCase)
            };
            tagItem.Click += (_, _) =>
            {
                _app.Store.SetTag(item.Id, tagHex);
                _model.Refresh();
                RefreshView();
            };
            tagsSubmenu.Items.Add(tagItem);
        }
        menu.Items.Add(tagsSubmenu);

        menu.Items.Add(new Separator());

        var editTitleItem = new MenuItem { Header = "Editar título...", InputGestureText = "F2" };
        editTitleItem.Click += (_, _) =>
        {
            var text = TextInputDialog.Prompt("Editar título", item.Title ?? "", multiline: false);
            if (text is not null)
            {
                _app.Store.SetTitle(item.Id, string.IsNullOrWhiteSpace(text) ? null : text.Trim());
                _model.Refresh();
                RefreshView();
            }
        };
        menu.Items.Add(editTitleItem);

        var editContentItem = new MenuItem { Header = "Editar conteúdo...", InputGestureText = "Ctrl+E" };
        editContentItem.Click += (_, _) =>
        {
            var text = TextInputDialog.Prompt("Editar conteúdo", item.Content, multiline: true);
            if (!string.IsNullOrWhiteSpace(text))
            {
                _app.Store.TryUpdateContent(item.Id, item.Kind, text);
                _model.Refresh();
                RefreshView();
            }
        };
        menu.Items.Add(editContentItem);

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem { Header = "Excluir", InputGestureText = "Delete" };
        deleteItem.Click += (_, _) =>
        {
            _app.Store.Delete(item.Id);
            _model.Refresh();
            RefreshView();
        };
        menu.Items.Add(deleteItem);

        menu.PlacementTarget = target;
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out System.Drawing.Point point);
}
