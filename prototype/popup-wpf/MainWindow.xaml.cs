// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

// PROTOTYPE (throwaway): "Three variants of the Copyous popup — A faithful
// cards, B vertical list, C command palette — switchable via the bottom bar,
// Ctrl+1/2/3, or 1/2/3 (when search is not focused); Dark/Light/HighContrast
// via the Theme button or F6. Does the WPF popup look/behave like Copyous?"
// Rules: in-memory mock data only, NO real clipboard/tray/persistence writes,
// keyboard mirrors Copyous (arrows/Tab/Home/End, Enter copy, Ctrl+Enter default
// action, Esc close). Verdict goes on issue 07 before capture to a
// prototype/<name> branch. See prototype/popup-wpf/README.md.
namespace PopupProto
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out WinPoint lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct WinPoint { public int X; public int Y; }

        private class Theme
        {
            public Brush Bg; public Brush Card; public Brush Fg;
            public Brush Muted; public Brush Search; public Brush Line; public Brush Accent;
        }

        private List<MockItem> all = MockData.All();
        private List<MockItem> visible = new List<MockItem>();
        private int selected = 0;
        private char variant = 'A';
        private string themeName = "Dark";
        private Theme T;
        private string lastAction = "ready.";

        public MainWindow()
        {
            InitializeComponent();
            T = ThemeFor(themeName);
            RenderAll();
        }

        // ---------- theme ----------

        private static Brush B(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex);
        }

        private static Theme ThemeFor(string name)
        {
            Theme t = new Theme();
            if (name == "Light")
            {
                t.Bg = B("#FAFAFB"); t.Card = B("#FFFFFF"); t.Fg = B("#222226");
                t.Muted = B("#6E6E76"); t.Search = B("#FFFFFF"); t.Line = B("#D8D8DE");
                t.Accent = B("#3584E4");
            }
            else if (name == "HighContrast")
            {
                t.Bg = B("#000000"); t.Card = B("#000000"); t.Fg = B("#FFFFFF");
                t.Muted = B("#E0E0E0"); t.Search = B("#000000"); t.Line = B("#FFFFFF");
                t.Accent = B("#FFFF00");
            }
            else // Dark (Copyous custom-bg default rgb(54,54,58) / card rgb(71,71,76))
            {
                t.Bg = B("#36363A"); t.Card = B("#47474C"); t.Fg = B("#FFFFFF");
                t.Muted = B("#A0A0A8"); t.Search = B("#47474C"); t.Line = B("#5A5A61");
                t.Accent = B("#3584E4");
            }
            return t;
        }

        private void ApplyTheme()
        {
            T = ThemeFor(themeName);
            RootBorder.Background = T.Bg;
            RootBorder.BorderBrush = T.Line;
            SearchBorder.Background = T.Search;
            SearchBorder.BorderBrush = T.Line;
            SearchBox.Foreground = T.Fg;
            SearchBox.CaretBrush = T.Fg;
            SettingsBtn.Foreground = T.Fg;
            IncognitoBtn.Foreground = T.Fg;
            ClearBtn.Foreground = T.Fg;
            PinFilterBtn.Foreground = T.Fg;
            SwitcherBorder.BorderBrush = T.Line;
            VariantLabel.Foreground = T.Muted;
            StatusLine.Foreground = T.Muted;
            ThemeBtn.Content = "Theme: " + themeName;
        }

        private void CycleTheme()
        {
            if (themeName == "Dark") themeName = "Light";
            else if (themeName == "Light") themeName = "HighContrast";
            else themeName = "Dark";
            lastAction = "theme -> " + themeName + ".";
            RenderAll();
        }

        // ---------- filtering + render ----------

        private void Refilter()
        {
            string q = SearchBox.Text.Trim().ToLowerInvariant();
            bool pinsOnly = PinFilterBtn.IsChecked == true;
            visible.Clear();
            foreach (MockItem item in all)
            {
                if (pinsOnly && !item.Pinned) continue;
                if (q.Length > 0)
                {
                    string hay = (item.Title + "\n" + item.Body).ToLowerInvariant();
                    if (!hay.Contains(q)) continue;
                }
                visible.Add(item);
            }
            if (selected >= visible.Count) selected = visible.Count - 1;
            if (selected < 0 && visible.Count > 0) selected = 0;
        }

        private void RenderAll()
        {
            ApplyTheme();
            Refilter();
            CardsPanel.Children.Clear();
            RowsPanelB.Children.Clear();
            RowsPanelC.Children.Clear();
            for (int i = 0; i < visible.Count; i++)
            {
                CardsPanel.Children.Add(BuildCard(visible[i], i == selected));
                RowsPanelB.Children.Add(BuildRowB(visible[i], i == selected));
                RowsPanelC.Children.Add(BuildRowC(visible[i], i == selected));
            }
            ScrollA.Visibility = variant == 'A' ? Visibility.Visible : Visibility.Collapsed;
            ScrollB.Visibility = variant == 'B' ? Visibility.Visible : Visibility.Collapsed;
            ScrollC.Visibility = variant == 'C' ? Visibility.Visible : Visibility.Collapsed;
            VariantLabel.Text = variant == 'A' ? "A (Cards — Copyous-faithful)"
                : variant == 'B' ? "B (Vertical list — Compact)" : "C (Command palette)";
            if (selected >= 0 && selected < visible.Count)
            {
                FrameworkElement el = null;
                if (variant == 'A' && selected < CardsPanel.Children.Count)
                    el = CardsPanel.Children[selected] as FrameworkElement;
                if (variant == 'B' && selected < RowsPanelB.Children.Count)
                    el = RowsPanelB.Children[selected] as FrameworkElement;
                if (variant == 'C' && selected < RowsPanelC.Children.Count)
                    el = RowsPanelC.Children[selected] as FrameworkElement;
                if (el != null) el.BringIntoView();
            }
            int pinned = 0;
            foreach (MockItem item in all) if (item.Pinned) pinned++;
            string q = SearchBox.Text.Trim();
            string pos = (selected + 1) + "/" + visible.Count;
            if (visible.Count == 0) pos = "0/0";
            StatusLine.Text = "[PROTOTYPE] variant " + VariantLabel.Text
                + " · theme " + themeName + " · selected " + pos
                + " · " + pinned + " pinned · filter '" + q + "'"
                + (PinFilterBtn.IsChecked == true ? " + pins-only" : "")
                + (IncognitoBtn.IsChecked == true ? " · INCOGNITO (mock)" : "")
                + " — last: " + lastAction;
        }

        private Border TagStrip(MockItem item)
        {
            Border strip = new Border();
            strip.Width = 4;
            strip.CornerRadius = new CornerRadius(2);
            strip.Background = B(item.TagHex);
            strip.Margin = new Thickness(0, 2, 8, 2);
            return strip;
        }

        private TextBlock HeaderLine(MockItem item, double size)
        {
            TextBlock tb = new TextBlock();
            tb.Text = MockData.IconFor(item.Kind) + "  " + item.Meta
                + (item.Pinned ? "   📌" : "");
            tb.FontSize = size;
            tb.Foreground = T.Muted;
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            return tb;
        }

        private FrameworkElement BuildPreview(MockItem item, double bodySize)
        {
            if (item.Kind == ItemKind.Image)
            {
                Border img = new Border();
                img.CornerRadius = new CornerRadius(6);
                LinearGradientBrush grad = new LinearGradientBrush();
                grad.StartPoint = new Point(0, 0);
                grad.EndPoint = new Point(1, 1);
                grad.GradientStops.Add(new GradientStop(((SolidColorBrush)B("#3a944a")).Color, 0));
                grad.GradientStops.Add(new GradientStop(((SolidColorBrush)B("#3584e4")).Color, 1));
                img.Background = grad;
                img.MinHeight = 60;
                return img;
            }
            if (item.Kind == ItemKind.Character)
            {
                TextBlock ch = new TextBlock();
                ch.Text = item.Body;
                ch.FontSize = 44;
                ch.HorizontalAlignment = HorizontalAlignment.Center;
                ch.Foreground = T.Fg;
                return ch;
            }
            if (item.Kind == ItemKind.Color)
            {
                Border swatch = new Border();
                swatch.CornerRadius = new CornerRadius(6);
                swatch.Background = B(item.Body);
                swatch.MinHeight = 44;
                TextBlock hex = new TextBlock();
                hex.Text = item.Body;
                hex.FontSize = bodySize;
                hex.Foreground = B("#FFFFFF");
                hex.HorizontalAlignment = HorizontalAlignment.Center;
                hex.VerticalAlignment = VerticalAlignment.Center;
                swatch.Child = hex;
                return swatch;
            }
            TextBlock tb = new TextBlock();
            tb.Text = item.Body;
            tb.FontSize = bodySize;
            tb.Foreground = T.Fg;
            tb.TextWrapping = TextWrapping.Wrap;
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            if (item.Kind == ItemKind.Code) tb.FontFamily = new FontFamily("Consolas");
            if (item.Kind == ItemKind.Link)
            {
                tb.FontWeight = FontWeights.SemiBold;
            }
            return tb;
        }

        private Border BuildCard(MockItem item, bool isSelected)
        {
            Border outer = new Border();
            outer.Width = 250;
            outer.Height = 170;
            outer.Margin = new Thickness(0, 0, 10, 0);
            outer.CornerRadius = new CornerRadius(8);
            outer.Background = T.Card;
            outer.BorderBrush = isSelected ? T.Accent : T.Line;
            outer.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
            DockPanel dock = new DockPanel();
            dock.Margin = new Thickness(10);
            dock.Children.Add(HeaderLine(item, 11));
            DockPanel.SetDock(dock.Children[0], Dock.Top);
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.Children.Add(TagStrip(item));
            FrameworkElement preview = BuildPreview(item, 12);
            preview.Width = 196; // Border previews (image/color) need explicit width in a horizontal StackPanel
            row.Children.Add(preview);
            dock.Children.Add(row);
            outer.Child = dock;
            return outer;
        }

        private Border BuildRowB(MockItem item, bool isSelected)
        {
            Border outer = BuildRowShell(isSelected, 64);
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.Children.Add(TagStrip(item));
            TextBlock icon = new TextBlock();
            icon.Text = MockData.IconFor(item.Kind);
            icon.FontSize = 20;
            icon.Width = 36;
            icon.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(icon);
            StackPanel mid = new StackPanel();
            mid.VerticalAlignment = VerticalAlignment.Center;
            TextBlock title = new TextBlock();
            title.Text = item.Title + (item.Pinned ? "  📌" : "");
            title.FontSize = 13;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = T.Fg;
            mid.Children.Add(title);
            TextBlock meta = new TextBlock();
            meta.Text = item.Meta;
            meta.FontSize = 11;
            meta.Foreground = T.Muted;
            mid.Children.Add(meta);
            row.Children.Add(mid);
            outer.Child = row;
            return outer;
        }

        private Border BuildRowC(MockItem item, bool isSelected)
        {
            Border outer = BuildRowShell(isSelected, 46);
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            TextBlock icon = new TextBlock();
            icon.Text = MockData.IconFor(item.Kind);
            icon.FontSize = 16;
            icon.Width = 30;
            icon.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(icon);
            TextBlock line = new TextBlock();
            string first = item.Body.Split('\n')[0];
            line.Text = item.Title + "   ·   " + first;
            line.FontSize = 13;
            line.Foreground = T.Fg;
            line.VerticalAlignment = VerticalAlignment.Center;
            line.TextTrimming = TextTrimming.CharacterEllipsis;
            row.Children.Add(line);
            TextBlock hint = new TextBlock();
            hint.Text = "↵";
            hint.Foreground = T.Muted;
            hint.VerticalAlignment = VerticalAlignment.Center;
            hint.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(hint);
            outer.Child = row;
            return outer;
        }

        private Border BuildRowShell(bool isSelected, double height)
        {
            Border outer = new Border();
            outer.Height = height;
            outer.Margin = new Thickness(0, 0, 0, 6);
            outer.Padding = new Thickness(10, 4, 10, 4);
            outer.CornerRadius = new CornerRadius(8);
            outer.Background = T.Card;
            outer.BorderBrush = isSelected ? T.Accent : T.Line;
            outer.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
            return outer;
        }

        // ---------- selection + actions (mock: status line only) ----------

        private void MoveSelection(int delta)
        {
            if (visible.Count == 0) return;
            selected = (selected + delta + visible.Count) % visible.Count;
            lastAction = "moved to " + (selected + 1) + "/" + visible.Count + ".";
            RenderAll();
        }

        private void CopySelected()
        {
            if (selected < 0 || selected >= visible.Count)
            {
                lastAction = "nothing to copy (empty filter).";
            }
            else
            {
                MockItem item = visible[selected];
                lastAction = "MOCK copy (" + item.Kind + "): " + item.Title
                    + " — real clipboard untouched.";
            }
            RenderAll();
        }

        private void DefaultActionSelected()
        {
            if (selected < 0 || selected >= visible.Count)
            {
                lastAction = "nothing to act on.";
            }
            else
            {
                MockItem item = visible[selected];
                lastAction = "MOCK default action [" + item.DefaultAction + "] on: "
                    + item.Title + ".";
            }
            RenderAll();
        }

        private void SetVariant(char v)
        {
            variant = v;
            if (v == 'A') this.Width = 900;
            else if (v == 'B') this.Width = 480;
            else this.Width = 620;
            lastAction = "variant -> " + v + ".";
            RenderAll();
        }

        // ---------- events ----------

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WinPoint p;
            if (GetCursorPos(out p))
            {
                PresentationSource src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null)
                {
                    Point dip = src.CompositionTarget.TransformFromDevice.Transform(
                        new Point(p.X, p.Y));
                    double left = dip.X + 12;
                    double top = dip.Y + 12;
                    Rect work = SystemParameters.WorkArea;
                    if (left + this.Width > work.Right) left = work.Right - this.Width;
                    if (top + this.Height > work.Bottom) top = work.Bottom - this.Height;
                    if (left < work.Left) left = work.Left;
                    if (top < work.Top) top = work.Top;
                    this.Left = left;
                    this.Top = top;
                }
            }
            SearchBox.Focus();
        }

        private static bool DigitKey(Key key, out int n)
        {
            n = 0;
            if (key >= Key.D1 && key <= Key.D3) { n = (int)key - (int)Key.D1 + 1; return true; }
            if (key >= Key.NumPad1 && key <= Key.NumPad3) { n = (int)key - (int)Key.NumPad1 + 1; return true; }
            return false;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                lastAction = "closed (Esc).";
                this.Close();
                return;
            }
            if (e.Key == Key.F6)
            {
                CycleTheme();
                e.Handled = true;
                return;
            }
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            int digit;
            if (ctrl && DigitKey(e.Key, out digit))
            {
                SetVariant("ABC"[digit - 1]);
                e.Handled = true;
                return;
            }
            bool inSearch = SearchBox.IsFocused;
            if (inSearch)
            {
                if (e.Key == Key.Up) { MoveSelection(-1); e.Handled = true; }
                else if (e.Key == Key.Down) { MoveSelection(1); e.Handled = true; }
                else if (e.Key == Key.Enter) { CopySelected(); e.Handled = true; }
                return;
            }
            if (!ctrl && DigitKey(e.Key, out digit))
            {
                SetVariant("ABC"[digit - 1]);
                e.Handled = true;
                return;
            }
            switch (e.Key)
            {
                case Key.Left:
                case Key.Up:
                    MoveSelection(-1); e.Handled = true; break;
                case Key.Right:
                case Key.Down:
                    MoveSelection(1); e.Handled = true; break;
                case Key.Home:
                    selected = 0; lastAction = "moved to first."; RenderAll(); e.Handled = true; break;
                case Key.End:
                    selected = visible.Count - 1; lastAction = "moved to last."; RenderAll(); e.Handled = true; break;
                case Key.Tab:
                    MoveSelection((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1);
                    e.Handled = true; break;
                case Key.Enter:
                    if (ctrl) DefaultActionSelected(); else CopySelected();
                    e.Handled = true; break;
                case Key.Delete:
                    lastAction = visible.Count > 0
                        ? "MOCK delete: " + visible[selected].Title + " (Shift=force)."
                        : "nothing to delete.";
                    RenderAll(); e.Handled = true; break;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            selected = 0;
            RenderAll();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            lastAction = PinFilterBtn.IsChecked == true ? "pins-only filter on." : "pins-only filter off.";
            selected = 0;
            RenderAll();
        }

        private void Incognito_Changed(object sender, RoutedEventArgs e)
        {
            lastAction = IncognitoBtn.IsChecked == true
                ? "incognito ON — capture would suspend (mock)."
                : "incognito off.";
            RenderAll();
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            int kept = 0;
            foreach (MockItem item in all) if (item.Pinned) kept++;
            lastAction = "MOCK clear: would remove " + (all.Count - kept)
                + ", keep " + kept + " pinned.";
            RenderAll();
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            lastAction = "settings would open (out of prototype scope).";
            RenderAll();
        }

        private void VariantA_Click(object sender, RoutedEventArgs e) { SetVariant('A'); }
        private void VariantB_Click(object sender, RoutedEventArgs e) { SetVariant('B'); }
        private void VariantC_Click(object sender, RoutedEventArgs e) { SetVariant('C'); }
        private void PrevVariant_Click(object sender, RoutedEventArgs e)
        {
            SetVariant(variant == 'A' ? 'C' : (char)(variant - 1));
        }
        private void NextVariant_Click(object sender, RoutedEventArgs e)
        {
            SetVariant(variant == 'C' ? 'A' : (char)(variant + 1));
        }
        private void ThemeBtn_Click(object sender, RoutedEventArgs e) { CycleTheme(); }
    }
}
