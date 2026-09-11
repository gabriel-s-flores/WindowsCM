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
        // Re-enable auto-height for a fresh measure: the previous open
        // froze SizeToContent=Manual so search filtering never resizes.
        SizeToContent = SizeToContent.Height;
        Width = PopupSizing.FixedWidth;
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
        // Freeze: filtering the search must not resize or reposition the
        // window (ticket 21) — the list scrolls internally. Pin the
        // laid-out size explicitly so Manual keeps the same size, no jump.
        Width = ActualWidth;
        Height = PopupSizing.ClampHeight(ActualHeight);
        SizeToContent = SizeToContent.Manual;
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
        PinsButton.FontWeight = _model.PinsOnly ? FontWeights.Bold : FontWeights.Normal;
        IncognitoButton.FontWeight = _model.IsIncognito ? FontWeights.Bold : FontWeights.Normal;
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
        // Post-layout ground truth (Show+UpdateLayout already ran, so the
        // first open measures the same as later ones). Fallbacks keep the
        // window visible if layout ever reports zero.
        var placedWidth = ActualWidth > 0 ? ActualWidth : PopupSizing.FixedWidth;
        var placedHeight = PopupSizing.ClampHeight(ActualHeight > 0 ? ActualHeight : PopupSizing.MaxHeight);
        var (left, top) = PopupPlacement.PlaceAtCursor(
            cursorDips.X, cursorDips.Y, placedWidth, placedHeight,
            new WorkArea(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y));
        Left = left;
        Top = top;
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
            isTitle ? "Edit title" : "Edit item",
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

    private void ShowActionsMenu()
    {
        if (_model.SelectedItem is not { } item)
        {
            return;
        }
        var applicable = _app.Executor.Applicable(_app.Actions, item);
        var menu = new ContextMenu();
        if (applicable.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "No actions apply", IsEnabled = false });
        }
        foreach (var action in applicable)
        {
            var captured = action;
            var menuEntry = new MenuItem { Header = captured.Name };
            menuEntry.Click += (_, _) => _ = _app.RunActionAsync(captured, item);
            menu.Items.Add(menuEntry);
        }
        menu.PlacementTarget = ItemsList;
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
