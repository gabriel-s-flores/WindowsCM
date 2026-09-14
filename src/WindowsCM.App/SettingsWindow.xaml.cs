// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using Control = System.Windows.Controls.Control;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using KeyInterop = System.Windows.Input.KeyInterop;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WindowsCM.Core.History;
using WindowsCM.Core.Hotkeys;
using WindowsCM.Core.Lifecycle;
using WindowsCM.Core.Lifecycle.Win32;
using WindowsCM.Core.Localization;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Release;
using WindowsCM.Core.Settings;

namespace WindowsCM.App;

// Modern Fluent Settings Window (Windows 11).
// Features sidebar navigation, history limit slider (10..100, recommended 100),
// direct cache cleaning with live feedback, and real-time custom color configuration for all clipboard item types.
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly string _settingsPath;
    private readonly HotkeyService? _hotkeys;
    private readonly IntPtr _hotkeyHwnd;
    private readonly Action? _onSettingsLiveUpdated;
    private readonly Action _onClosed;
    private readonly Action? _onShowWelcome;
    private ColorScheme _currentScheme;
    private bool _loading = true;
    private Button? _activeNavBtn;
    private readonly HotkeyRecorder _recorder = new(KeyCodes.Layout);
    private HotkeySlot _recordingSlot;
    private Button? _recordingButton;
    private object? _recordButtonContent;

    private enum HotkeyStatusKind
    {
        Info,
        Listening,
        Success,
        Error,
    }

    public SettingsWindow(
        AppSettings settings,
        string settingsPath,
        HotkeyService? hotkeys,
        IntPtr hotkeyHwnd,
        ColorScheme effectiveScheme,
        Action? onSettingsLiveUpdated,
        Action onClosed,
        Action? onShowWelcome = null)
    {
        _settings = settings;
        _settingsPath = settingsPath;
        _hotkeys = hotkeys;
        _hotkeyHwnd = hotkeyHwnd;
        _onSettingsLiveUpdated = onSettingsLiveUpdated;
        _onClosed = onClosed;
        _onShowWelcome = onShowWelcome;

        InitializeComponent();

        var workArea = SystemParameters.WorkArea;
        if (workArea.Height > 0)
        {
            MaxHeight = workArea.Height * 0.94;
            if (Height > MaxHeight)
            {
                Height = Math.Max(MinHeight, MaxHeight);
            }
        }
        if (workArea.Width > 0)
        {
            MaxWidth = workArea.Width * 0.95;
            if (Width > MaxWidth)
            {
                Width = Math.Max(MinWidth, MaxWidth);
            }
        }

        ApplyTheme(effectiveScheme);

        // Theme scheme
        ThemeSchemeCombo.SelectedIndex = _settings.Theme.Scheme switch
        {
            ColorScheme.Dark => 1,
            ColorScheme.Light => 2,
            ColorScheme.HighContrast => 3,
            _ => 0,
        };

        // Language
        LanguageCombo.SelectedIndex = _settings.Language switch
        {
            AppLanguage.English => 1,
            AppLanguage.Portuguese => 2,
            _ => 0,
        };

        // General / History
        HistoryLimitSlider.Value = Math.Clamp(_settings.History.MaxItems, SettingLimits.HistoryLengthMin, SettingLimits.HistoryLengthMax);
        HistoryLimitValueText.Text = LocalizationManager.Strings.SettingsHistoryLimitBadge(_settings.History.MaxItems);

        AutostartCheck.IsChecked = AutostartManager.IsEnabled(new RegistryRunKeyStore());

        EndOfSessionCombo.SelectedIndex = (int)_settings.History.EndOfSession;

        // Shortcuts
        OpenGestureBox.Text = settings.Shortcuts.OpenGesture;
        IncognitoGestureBox.Text = settings.Shortcuts.IncognitoGesture;
        ShowHotkeyInfo();

        // Layout & Placement
        LargeOrientationCombo.SelectedIndex = _settings.Dialog.Orientation == DialogOrientation.Vertical ? 1 : 0;
        LargeHorizontalPosCombo.SelectedIndex = _settings.Dialog.LargeHorizontalPosition == LargeHorizontalPosition.Top ? 1 : 0;
        LargeVerticalPosCombo.SelectedIndex = _settings.Dialog.LargeVerticalPosition == LargeVerticalPosition.Right ? 1 : 0;
        LargeHorizontalOrderCombo.SelectedIndex = _settings.Dialog.LargeHorizontalOrder == HorizontalItemOrder.RecentOnRight ? 1 : 0;
        LargeVerticalOrderCombo.SelectedIndex = _settings.Dialog.LargeVerticalOrder == VerticalItemOrder.RecentOnBottom ? 1 : 0;
        LargeHorizontalScrollbarPosCombo.SelectedIndex = _settings.Dialog.HorizontalScrollbarPosition == HorizontalScrollbarPosition.Top ? 1 : 0;
        LargeVerticalScrollbarPosCombo.SelectedIndex = _settings.Dialog.VerticalScrollbarPosition == VerticalScrollbarPosition.Left ? 1 : 0;

        CompactOrientationCombo.SelectedIndex = _settings.Dialog.CompactOrientation == DialogOrientation.Horizontal ? 1 : 0;
        CompactVerticalOrderCombo.SelectedIndex = _settings.Dialog.CompactVerticalOrder == VerticalItemOrder.RecentOnBottom ? 1 : 0;
        CompactHorizontalOrderCombo.SelectedIndex = _settings.Dialog.CompactHorizontalOrder == HorizontalItemOrder.RecentOnRight ? 1 : 0;
        CompactVerticalScrollbarPosCombo.SelectedIndex = _settings.Dialog.VerticalScrollbarPosition == VerticalScrollbarPosition.Left ? 1 : 0;
        CompactHorizontalScrollbarPosCombo.SelectedIndex = _settings.Dialog.HorizontalScrollbarPosition == HorizontalScrollbarPosition.Top ? 1 : 0;

        UpdateLayoutControlsVisibility();
        UpdateMockPreview();

        // Colors
        PopulateUnifiedColorControls();

        // System & Diagnostics
        var info = DiagnosticsInfo.Collect(settings, settingsPath);
        UpdateDiagnosticsTexts(info);
        CreditsText.Text = string.Join("\n", AboutCredits.Upstream
                .Select(u => $"{u.Name} ({u.License}) — {u.Role}"))
            + "\n" + string.Join("\n", AboutCredits.Libraries
                .Select(l => $"{l.Name} ({l.License}){(l.Version is null ? "" : " " + l.Version)}"));

        HighlightNavButton(NavBtnGeneral);

        _loading = false;
    }

    private void UpdateDiagnosticsTexts(DiagnosticsInfo? info = null)
    {
        info ??= DiagnosticsInfo.Collect(_settings, _settingsPath);
        var strings = LocalizationManager.Strings;
        TrayGuidanceText.Text = strings.SettingsTrayGuidanceBody;
        PathsText.Text = $"{strings.SettingsPathsLabelData}: {info.DataDir}\n" +
            $"{strings.SettingsPathsLabelConfig}: {info.ConfigDir}\n" +
            $"{strings.SettingsPathsLabelCache}: {info.CacheDir}\n" +
            $"{strings.SettingsPathsLabelDb}: {info.DatabasePath}\n" +
            $"{strings.SettingsPathsLabelActions}: {info.ActionsPath}\n" +
            $"{strings.SettingsPathsLabelSettings}: {info.SettingsPath}";
        VersionsText.Text = $"WindowsCM {info.AppVersion} ({AboutCredits.LicenseId})\n" +
            $".NET {info.DotNetVersion}\n{strings.SettingsPathsLabelDb} SQLite {info.SqliteVersion}";
    }

    public void UpdateLanguage()
    {
        var strings = LocalizationManager.Strings;
        Title = strings.SettingsTitle;
        ThemeStatusText.Text = _currentScheme switch
        {
            ColorScheme.Light => strings.SettingsThemeStatusLight,
            ColorScheme.Dark => strings.SettingsThemeStatusDark,
            ColorScheme.HighContrast => strings.SettingsThemeStatusHighContrast,
            _ => strings.SettingsThemeStatusFluent,
        };
        HistoryLimitValueText.Text = strings.SettingsHistoryLimitBadge(_settings.History.MaxItems);
        StopRecording();
        ShowHotkeyInfo();
        UpdateDiagnosticsTexts();
        PopulateUnifiedColorControls();
        UpdateMockPreview();
    }

    public void ApplyTheme(ColorScheme scheme)
    {
        _currentScheme = scheme;
        var themeDict = PopupThemeBrushes.CreateThemeDictionary(scheme, _settings.ItemColors);
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(themeDict);

        ThemeStatusText.Text = scheme switch
        {
            ColorScheme.Light => LocalizationManager.Strings.SettingsThemeStatusLight,
            ColorScheme.Dark => LocalizationManager.Strings.SettingsThemeStatusDark,
            ColorScheme.HighContrast => LocalizationManager.Strings.SettingsThemeStatusHighContrast,
            _ => LocalizationManager.Strings.SettingsThemeStatusFluent,
        };

        if (!_loading)
        {
            PopulateUnifiedColorControls();
        }

        HighlightNavButton(_activeNavBtn ?? NavBtnGeneral);
    }

    public void ApplyTheme(bool isLight) =>
        ApplyTheme(isLight ? ColorScheme.Light : ColorScheme.Dark);

    private void OnThemeSchemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;

        var selected = ThemeSchemeCombo.SelectedIndex switch
        {
            1 => ColorScheme.Dark,
            2 => ColorScheme.Light,
            3 => ColorScheme.HighContrast,
            _ => ColorScheme.System,
        };

        _settings.Theme.Scheme = selected;
        try
        {
            SettingsStore.Save(_settingsPath, _settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        _onSettingsLiveUpdated?.Invoke();
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;

        var selected = LanguageCombo.SelectedIndex switch
        {
            1 => AppLanguage.English,
            2 => AppLanguage.Portuguese,
            _ => AppLanguage.System,
        };

        if (_settings.Language == selected) return;

        _settings.Language = selected;
        LocalizationManager.CurrentLanguage = selected;

        try
        {
            SettingsStore.Save(_settingsPath, _settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        if (System.Windows.Application.Current is App app)
        {
            app.UpdateLanguage(selected);
        }

        UpdateLanguage();
        _onSettingsLiveUpdated?.Invoke();
    }

    // --- NAVIGATION ---

    private void HighlightNavButton(Button activeBtn)
    {
        _activeNavBtn = activeBtn;
        var buttons = new[] { NavBtnGeneral, NavBtnLayout, NavBtnColors, NavBtnStorage, NavBtnShortcuts, NavBtnAbout };
        foreach (var btn in buttons)
        {
            var isSelected = btn == activeBtn;
            if (btn.Content is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is TextBlock tb)
                    {
                        if (tb.FontFamily.Source.Contains("Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase))
                        {
                            tb.FontWeight = FontWeights.Normal;
                        }
                        else
                        {
                            tb.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
                        }
                    }
                }
            }

            if (isSelected)
            {
                btn.SetResourceReference(Button.BackgroundProperty, "IconButtonHoverBackgroundBrush");
            }
            else
            {
                btn.ClearValue(Button.BackgroundProperty);
            }
        }
    }

    private void OnNavGeneralClicked(object sender, RoutedEventArgs e)
    {
        ShowPanel(PanelGeneral);
        HighlightNavButton(NavBtnGeneral);
    }

    private void OnNavLayoutClicked(object sender, RoutedEventArgs e)
    {
        ShowPanel(PanelLayout);
        HighlightNavButton(NavBtnLayout);
    }

    private void OnNavColorsClicked(object sender, RoutedEventArgs e)
    {
        ShowPanel(PanelColors);
        HighlightNavButton(NavBtnColors);
    }

    private void OnNavStorageClicked(object sender, RoutedEventArgs e)
    {
        ShowPanel(PanelStorage);
        HighlightNavButton(NavBtnStorage);
    }

    private void OnNavShortcutsClicked(object sender, RoutedEventArgs e)
    {
        ShowPanel(PanelShortcuts);
        HighlightNavButton(NavBtnShortcuts);
    }

    private void OnNavAboutClicked(object sender, RoutedEventArgs e)
    {
        ShowPanel(PanelAbout);
        HighlightNavButton(NavBtnAbout);
    }

    private void ShowPanel(UIElement panelToShow)
    {
        PanelGeneral.Visibility = panelToShow == PanelGeneral ? Visibility.Visible : Visibility.Collapsed;
        PanelLayout.Visibility = panelToShow == PanelLayout ? Visibility.Visible : Visibility.Collapsed;
        PanelColors.Visibility = panelToShow == PanelColors ? Visibility.Visible : Visibility.Collapsed;
        PanelStorage.Visibility = panelToShow == PanelStorage ? Visibility.Visible : Visibility.Collapsed;
        PanelShortcuts.Visibility = panelToShow == PanelShortcuts ? Visibility.Visible : Visibility.Collapsed;
        PanelAbout.Visibility = panelToShow == PanelAbout ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- LAYOUT & PLACEMENT ---

    private int _previewMode = 0; // 0 = Large, 1 = Compact

    private void UpdateLayoutControlsVisibility()
    {
        var isLargeVertical = _settings.Dialog.Orientation == DialogOrientation.Vertical;
        LargeHorizontalPosCombo.Visibility = isLargeVertical ? Visibility.Collapsed : Visibility.Visible;
        LargeVerticalPosCombo.Visibility = isLargeVertical ? Visibility.Visible : Visibility.Collapsed;
        LargeHorizontalOrderCombo.Visibility = isLargeVertical ? Visibility.Collapsed : Visibility.Visible;
        LargeVerticalOrderCombo.Visibility = isLargeVertical ? Visibility.Visible : Visibility.Collapsed;
        LargeHorizontalScrollbarPosCombo.Visibility = isLargeVertical ? Visibility.Collapsed : Visibility.Visible;
        LargeVerticalScrollbarPosCombo.Visibility = isLargeVertical ? Visibility.Visible : Visibility.Collapsed;

        var isCompactHorizontal = _settings.Dialog.CompactOrientation == DialogOrientation.Horizontal;
        CompactVerticalOrderCombo.Visibility = isCompactHorizontal ? Visibility.Collapsed : Visibility.Visible;
        CompactHorizontalOrderCombo.Visibility = isCompactHorizontal ? Visibility.Visible : Visibility.Collapsed;
        CompactVerticalScrollbarPosCombo.Visibility = isCompactHorizontal ? Visibility.Collapsed : Visibility.Visible;
        CompactHorizontalScrollbarPosCombo.Visibility = isCompactHorizontal ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPreviewToggleLargeClicked(object sender, RoutedEventArgs e)
    {
        _previewMode = 0;
        PreviewToggleLargeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
        PreviewToggleLargeBtn.Foreground = System.Windows.Media.Brushes.White;
        PreviewToggleLargeBtn.FontWeight = FontWeights.SemiBold;

        PreviewToggleCompactBtn.Background = System.Windows.Media.Brushes.Transparent;
        PreviewToggleCompactBtn.SetResourceReference(Button.ForegroundProperty, "CardTitleBrush");
        PreviewToggleCompactBtn.FontWeight = FontWeights.Normal;

        UpdateMockPreview();
    }

    private void OnPreviewToggleCompactClicked(object sender, RoutedEventArgs e)
    {
        _previewMode = 1;
        PreviewToggleCompactBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
        PreviewToggleCompactBtn.Foreground = System.Windows.Media.Brushes.White;
        PreviewToggleCompactBtn.FontWeight = FontWeights.SemiBold;

        PreviewToggleLargeBtn.Background = System.Windows.Media.Brushes.Transparent;
        PreviewToggleLargeBtn.SetResourceReference(Button.ForegroundProperty, "CardTitleBrush");
        PreviewToggleLargeBtn.FontWeight = FontWeights.Normal;

        UpdateMockPreview();
    }

    private void OnLargeOrientationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.Orientation = LargeOrientationCombo.SelectedIndex == 1
            ? DialogOrientation.Vertical
            : DialogOrientation.Horizontal;
        UpdateLayoutControlsVisibility();
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void OnLargeHorizontalPosChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.LargeHorizontalPosition = LargeHorizontalPosCombo.SelectedIndex == 1
            ? LargeHorizontalPosition.Top
            : LargeHorizontalPosition.Bottom;
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void OnLargeVerticalPosChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.LargeVerticalPosition = LargeVerticalPosCombo.SelectedIndex == 1
            ? LargeVerticalPosition.Right
            : LargeVerticalPosition.Left;
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void OnLargeHorizontalOrderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.LargeHorizontalOrder = LargeHorizontalOrderCombo.SelectedIndex == 1
            ? HorizontalItemOrder.RecentOnRight
            : HorizontalItemOrder.RecentOnLeft;
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void OnLargeVerticalOrderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.LargeVerticalOrder = LargeVerticalOrderCombo.SelectedIndex == 1
            ? VerticalItemOrder.RecentOnBottom
            : VerticalItemOrder.RecentOnTop;
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void OnCompactOrientationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.CompactOrientation = CompactOrientationCombo.SelectedIndex == 1
            ? DialogOrientation.Horizontal
            : DialogOrientation.Vertical;
        UpdateLayoutControlsVisibility();
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void OnCompactVerticalOrderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.CompactVerticalOrder = CompactVerticalOrderCombo.SelectedIndex == 1
            ? VerticalItemOrder.RecentOnBottom
            : VerticalItemOrder.RecentOnTop;
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void OnCompactHorizontalOrderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.CompactHorizontalOrder = CompactHorizontalOrderCombo.SelectedIndex == 1
            ? HorizontalItemOrder.RecentOnRight
            : HorizontalItemOrder.RecentOnLeft;
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void OnLargeHorizontalScrollbarPosChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.HorizontalScrollbarPosition = LargeHorizontalScrollbarPosCombo.SelectedIndex == 1
            ? HorizontalScrollbarPosition.Top
            : HorizontalScrollbarPosition.Bottom;
        if (CompactHorizontalScrollbarPosCombo != null)
            CompactHorizontalScrollbarPosCombo.SelectedIndex = LargeHorizontalScrollbarPosCombo.SelectedIndex;
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void OnLargeVerticalScrollbarPosChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.VerticalScrollbarPosition = LargeVerticalScrollbarPosCombo.SelectedIndex == 1
            ? VerticalScrollbarPosition.Left
            : VerticalScrollbarPosition.Right;
        if (CompactVerticalScrollbarPosCombo != null)
            CompactVerticalScrollbarPosCombo.SelectedIndex = LargeVerticalScrollbarPosCombo.SelectedIndex;
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void OnCompactVerticalScrollbarPosChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.VerticalScrollbarPosition = CompactVerticalScrollbarPosCombo.SelectedIndex == 1
            ? VerticalScrollbarPosition.Left
            : VerticalScrollbarPosition.Right;
        if (LargeVerticalScrollbarPosCombo != null)
            LargeVerticalScrollbarPosCombo.SelectedIndex = CompactVerticalScrollbarPosCombo.SelectedIndex;
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void OnCompactHorizontalScrollbarPosChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Dialog.HorizontalScrollbarPosition = CompactHorizontalScrollbarPosCombo.SelectedIndex == 1
            ? HorizontalScrollbarPosition.Top
            : HorizontalScrollbarPosition.Bottom;
        if (LargeHorizontalScrollbarPosCombo != null)
            LargeHorizontalScrollbarPosCombo.SelectedIndex = CompactHorizontalScrollbarPosCombo.SelectedIndex;
        SaveLayoutSettings();
        UpdateMockPreview();
    }

    private void SaveLayoutSettings()
    {
        try
        {
            SettingsStore.Save(_settingsPath, _settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        _onSettingsLiveUpdated?.Invoke();
    }

    private void UpdateMockPreview()
    {
        if (MockPopupWindow == null || MockItemsPanel == null || MockFlowBadgeText == null)
        {
            return;
        }

        var cards = new[] { MockCard1, MockCard2, MockCard3, MockCard4 };

        if (_previewMode == 0) // Large Window
        {
            if (_settings.Dialog.Orientation == DialogOrientation.Horizontal)
            {
                MockPopupWindow.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                MockPopupWindow.VerticalAlignment = _settings.Dialog.LargeHorizontalPosition == LargeHorizontalPosition.Top
                    ? VerticalAlignment.Top
                    : VerticalAlignment.Bottom;
                MockPopupWindow.Width = double.NaN;
                MockPopupWindow.Height = 98;
                MockPopupWindow.Margin = new Thickness(10, 6, 10, 6);

                MockItemsPanel.Orientation = Orientation.Horizontal;
                foreach (var c in cards)
                {
                    if (c != null)
                    {
                        c.Width = 90;
                        c.Height = 56;
                        c.Margin = new Thickness(0, 0, 5, 0);
                    }
                }

                var recentOnLeft = _settings.Dialog.LargeHorizontalOrder == HorizontalItemOrder.RecentOnLeft;
                MockFlowBadgeText.Text = recentOnLeft
                    ? LocalizationManager.Strings.SettingsLayoutFlowHorizontalRecentLeft
                    : LocalizationManager.Strings.SettingsLayoutFlowHorizontalRecentRight;

                MockItemsPanel.Children.Clear();
                if (recentOnLeft)
                {
                    foreach (var c in cards) if (c != null) MockItemsPanel.Children.Add(c);
                }
                else
                {
                    for (int i = cards.Length - 1; i >= 0; i--)
                    {
                        if (cards[i] != null) MockItemsPanel.Children.Add(cards[i]);
                    }
                }
            }
            else // Vertical Large Window
            {
                MockPopupWindow.VerticalAlignment = VerticalAlignment.Stretch;
                MockPopupWindow.HorizontalAlignment = _settings.Dialog.LargeVerticalPosition == LargeVerticalPosition.Right
                    ? System.Windows.HorizontalAlignment.Right
                    : System.Windows.HorizontalAlignment.Left;
                MockPopupWindow.Width = 120;
                MockPopupWindow.Height = double.NaN;
                MockPopupWindow.Margin = new Thickness(8, 4, 8, 4);

                MockItemsPanel.Orientation = Orientation.Vertical;
                foreach (var c in cards)
                {
                    if (c != null)
                    {
                        c.Width = double.NaN;
                        c.Height = 22;
                        c.Margin = new Thickness(0, 0, 0, 2);
                    }
                }

                var recentOnTop = _settings.Dialog.LargeVerticalOrder == VerticalItemOrder.RecentOnTop;
                MockFlowBadgeText.Text = recentOnTop
                    ? LocalizationManager.Strings.SettingsLayoutFlowVerticalRecentTop
                    : LocalizationManager.Strings.SettingsLayoutFlowVerticalRecentBottom;

                MockItemsPanel.Children.Clear();
                if (recentOnTop)
                {
                    foreach (var c in cards) if (c != null) MockItemsPanel.Children.Add(c);
                }
                else
                {
                    for (int i = cards.Length - 1; i >= 0; i--)
                    {
                        if (cards[i] != null) MockItemsPanel.Children.Add(cards[i]);
                    }
                }
            }
        }
        else // Compact Menu
        {
            if (_settings.Dialog.CompactOrientation == DialogOrientation.Vertical)
            {
                MockPopupWindow.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                MockPopupWindow.VerticalAlignment = VerticalAlignment.Center;
                MockPopupWindow.Width = 110;
                MockPopupWindow.Height = 125;
                MockPopupWindow.Margin = new Thickness(0);

                MockItemsPanel.Orientation = Orientation.Vertical;
                foreach (var c in cards)
                {
                    if (c != null)
                    {
                        c.Width = double.NaN;
                        c.Height = 20;
                        c.Margin = new Thickness(0, 0, 0, 2);
                    }
                }

                var recentOnTop = _settings.Dialog.CompactVerticalOrder == VerticalItemOrder.RecentOnTop;
                MockFlowBadgeText.Text = recentOnTop
                    ? LocalizationManager.Strings.SettingsLayoutFlowVerticalRecentTop
                    : LocalizationManager.Strings.SettingsLayoutFlowVerticalRecentBottom;

                MockItemsPanel.Children.Clear();
                if (recentOnTop)
                {
                    foreach (var c in cards) if (c != null) MockItemsPanel.Children.Add(c);
                }
                else
                {
                    for (int i = cards.Length - 1; i >= 0; i--)
                    {
                        if (cards[i] != null) MockItemsPanel.Children.Add(cards[i]);
                    }
                }
            }
            else // Horizontal Compact
            {
                MockPopupWindow.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                MockPopupWindow.VerticalAlignment = VerticalAlignment.Center;
                MockPopupWindow.Width = 220;
                MockPopupWindow.Height = 88;
                MockPopupWindow.Margin = new Thickness(0);

                MockItemsPanel.Orientation = Orientation.Horizontal;
                foreach (var c in cards)
                {
                    if (c != null)
                    {
                        c.Width = 60;
                        c.Height = 48;
                        c.Margin = new Thickness(0, 0, 4, 0);
                    }
                }

                var recentOnLeft = _settings.Dialog.CompactHorizontalOrder == HorizontalItemOrder.RecentOnLeft;
                MockFlowBadgeText.Text = recentOnLeft
                    ? LocalizationManager.Strings.SettingsLayoutFlowCompactRecentLeft
                    : LocalizationManager.Strings.SettingsLayoutFlowCompactRecentRight;

                MockItemsPanel.Children.Clear();
                if (recentOnLeft)
                {
                    foreach (var c in cards) if (c != null) MockItemsPanel.Children.Add(c);
                }
                else
                {
                    for (int i = cards.Length - 1; i >= 0; i--)
                    {
                        if (cards[i] != null) MockItemsPanel.Children.Add(cards[i]);
                    }
                }
            }
        }

        // Update mock scrollbar indicators according to settings
        if (MockScrollBarH != null && MockScrollBarV != null && MockItemsScrollViewer != null)
        {
            var isH = (_previewMode == 0 && _settings.Dialog.Orientation == DialogOrientation.Horizontal) ||
                      (_previewMode == 1 && _settings.Dialog.CompactOrientation == DialogOrientation.Horizontal);

            if (isH)
            {
                MockScrollBarH.Visibility = Visibility.Visible;
                MockScrollBarV.Visibility = Visibility.Collapsed;

                var isTop = _settings.Dialog.HorizontalScrollbarPosition == HorizontalScrollbarPosition.Top;
                MockScrollBarH.VerticalAlignment = isTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                MockItemsScrollViewer.Margin = isTop ? new Thickness(0, 5, 0, 0) : new Thickness(0, 0, 0, 5);
            }
            else
            {
                MockScrollBarH.Visibility = Visibility.Collapsed;
                MockScrollBarV.Visibility = Visibility.Visible;

                var isLeft = _settings.Dialog.VerticalScrollbarPosition == VerticalScrollbarPosition.Left;
                MockScrollBarV.HorizontalAlignment = isLeft ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Right;
                MockItemsScrollViewer.Margin = isLeft ? new Thickness(5, 0, 0, 0) : new Thickness(0, 0, 5, 0);
            }
        }

        // Animate flow badge with a quick pulse
        var anim = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(250));
        MockFlowBadge.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // --- HISTORY & GENERAL ---

    private void OnHistoryLimitChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;

        var val = (int)Math.Round(HistoryLimitSlider.Value);
        HistoryLimitValueText.Text = LocalizationManager.Strings.SettingsHistoryLimitBadge(val);
        _settings.History.MaxItems = val;
        _onSettingsLiveUpdated?.Invoke();
    }

    private void OnResetHistoryLimitClicked(object sender, RoutedEventArgs e)
    {
        HistoryLimitSlider.Value = SettingLimits.HistoryLengthDefault;
    }

    private void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var store = new RegistryRunKeyStore();
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) return;

        if (AutostartCheck.IsChecked == true)
        {
            AutostartManager.Enable(store, exe);
        }
        else
        {
            AutostartManager.Disable(store);
        }
    }

    private void OnEndOfSessionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.History.EndOfSession = (EndOfSessionMode)EndOfSessionCombo.SelectedIndex;
    }

    // --- CACHE CLEANING ---

    private void OnClearCacheClicked(object sender, RoutedEventArgs e)
    {
        ClearCacheBtn.IsEnabled = false;
        try
        {
            var result = CacheCleaner.Clear(AppFolders.CacheDir());
            ClearCacheFeedbackBorder.Visibility = Visibility.Visible;
            var strings = LocalizationManager.Strings;

            if (result.Success)
            {
                if (result.FilesDeleted > 0)
                {
                    ClearCacheFeedbackText.Text = strings.SettingsClearCacheSuccess(result.FilesDeleted, FormatBytes(result.BytesFreed));
                }
                else
                {
                    ClearCacheFeedbackText.Text = strings.SettingsClearCacheAlreadyEmpty;
                }
            }
            else
            {
                ClearCacheFeedbackText.Text = strings.SettingsClearCacheWarning(result.ErrorMessage ?? strings.SettingsClearCacheDefaultError);
            }
        }
        finally
        {
            ClearCacheBtn.IsEnabled = true;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    // --- UNIFIED COLOR & CATEGORY CUSTOMIZATION ---

    private sealed class UnifiedTypeEntry
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string BadgeText { get; init; } = "";
        public string Glyph { get; init; } = "";
        public string? SubtitleHint { get; init; }
        public FileCategory? Category { get; init; }
        public bool HasExtensions => Category != null;
        public string DefaultColorHex { get; init; } = "";
        public Func<string> GetCurrentColor { get; init; } = () => "";
        public Action<string> SetColor { get; init; } = _ => { };
        public Action? Reset { get; init; }
        public Action? Delete { get; init; }
    }

    private void PopulateUnifiedColorControls()
    {
        FileCategoriesPanel.Children.Clear();
        var strings = LocalizationManager.Strings;

        var defaultCats = FileCategorySettings.CreateDefaultCategories().ToDictionary(c => c.Id);
        var catImages = _settings.FileCategories.Categories.FirstOrDefault(c => c.Id == "images");
        var catCode = _settings.FileCategories.Categories.FirstOrDefault(c => c.Id == "code");
        var catDocuments = _settings.FileCategories.Categories.FirstOrDefault(c => c.Id == "documents");
        var catSpreadsheets = _settings.FileCategories.Categories.FirstOrDefault(c => c.Id == "spreadsheets");
        var catPresentations = _settings.FileCategories.Categories.FirstOrDefault(c => c.Id == "presentations");
        var catAudio = _settings.FileCategories.Categories.FirstOrDefault(c => c.Id == "audio");
        var catVideo = _settings.FileCategories.Categories.FirstOrDefault(c => c.Id == "video");
        var catArchives = _settings.FileCategories.Categories.FirstOrDefault(c => c.Id == "archives");

        var entries = new List<UnifiedTypeEntry>();

        // 1. Imagens (Unifica ItemKind.Image + Categoria images)
        entries.Add(new UnifiedTypeEntry
        {
            Id = "images",
            Name = strings.CategoryImages,
            BadgeText = strings.KindImage,
            Glyph = "\uEB9F",
            Category = catImages,
            DefaultColorHex = "#16A34A",
            GetCurrentColor = () => _settings.ItemColors.GetCustomColor(ItemKind.Image)
                ?? catImages?.ColorHex ?? "#16A34A",
            SetColor = hex =>
            {
                _settings.ItemColors.SetCustomColor(ItemKind.Image, hex);
                if (catImages != null) catImages.ColorHex = hex;
            },
            Reset = () =>
            {
                _settings.ItemColors.Reset(ItemKind.Image);
                if (catImages != null)
                {
                    catImages.ColorHex = "#16A34A";
                    if (defaultCats.TryGetValue("images", out var def))
                        catImages.Extensions = [.. def.Extensions];
                }
            }
        });

        // 2. Códigos e Scripts (Unifica ItemKind.Code + Categoria code)
        entries.Add(new UnifiedTypeEntry
        {
            Id = "code",
            Name = strings.CategoryCode,
            BadgeText = strings.KindCode,
            Glyph = "\uE943",
            Category = catCode,
            DefaultColorHex = "#6366F1",
            GetCurrentColor = () => _settings.ItemColors.GetCustomColor(ItemKind.Code)
                ?? catCode?.ColorHex ?? "#6366F1",
            SetColor = hex =>
            {
                _settings.ItemColors.SetCustomColor(ItemKind.Code, hex);
                if (catCode != null) catCode.ColorHex = hex;
            },
            Reset = () =>
            {
                _settings.ItemColors.Reset(ItemKind.Code);
                if (catCode != null)
                {
                    catCode.ColorHex = "#6366F1";
                    if (defaultCats.TryGetValue("code", out var def))
                        catCode.Extensions = [.. def.Extensions];
                }
            }
        });

        // 3. Links e Páginas (ItemKind.Link)
        entries.Add(new UnifiedTypeEntry
        {
            Id = "link",
            Name = strings.CategoryLinks,
            BadgeText = strings.KindLink,
            Glyph = "\uE71B",
            DefaultColorHex = "#0067B8",
            SubtitleHint = strings.SettingsLinkHint,
            GetCurrentColor = () => _settings.ItemColors.GetCustomColor(ItemKind.Link) ?? "#0067B8",
            SetColor = hex => _settings.ItemColors.SetCustomColor(ItemKind.Link, hex),
            Reset = () => _settings.ItemColors.Reset(ItemKind.Link)
        });

        // 4. Documentos (Categoria documents)
        if (catDocuments != null)
        {
            entries.Add(new UnifiedTypeEntry
            {
                Id = "documents",
                Name = strings.CategoryDocuments,
                BadgeText = strings.BadgeDocument,
                Glyph = "\uE8A5",
                Category = catDocuments,
                DefaultColorHex = "#0078D4",
                GetCurrentColor = () => catDocuments.ColorHex,
                SetColor = hex => catDocuments.ColorHex = hex,
                Reset = () =>
                {
                    catDocuments.ColorHex = "#0078D4";
                    if (defaultCats.TryGetValue("documents", out var def))
                        catDocuments.Extensions = [.. def.Extensions];
                }
            });
        }

        // 5. Planilhas (Categoria spreadsheets)
        if (catSpreadsheets != null)
        {
            entries.Add(new UnifiedTypeEntry
            {
                Id = "spreadsheets",
                Name = strings.CategorySpreadsheets,
                BadgeText = strings.BadgeSpreadsheet,
                Glyph = "\uF0E3",
                Category = catSpreadsheets,
                DefaultColorHex = "#0D9488",
                GetCurrentColor = () => catSpreadsheets.ColorHex,
                SetColor = hex => catSpreadsheets.ColorHex = hex,
                Reset = () =>
                {
                    catSpreadsheets.ColorHex = "#0D9488";
                    if (defaultCats.TryGetValue("spreadsheets", out var def))
                        catSpreadsheets.Extensions = [.. def.Extensions];
                }
            });
        }

        // 6. Apresentações (Categoria presentations)
        if (catPresentations != null)
        {
            entries.Add(new UnifiedTypeEntry
            {
                Id = "presentations",
                Name = strings.CategoryPresentations,
                BadgeText = strings.BadgePresentation,
                Glyph = "\uE8AD",
                Category = catPresentations,
                DefaultColorHex = "#EA580C",
                GetCurrentColor = () => catPresentations.ColorHex,
                SetColor = hex => catPresentations.ColorHex = hex,
                Reset = () =>
                {
                    catPresentations.ColorHex = "#EA580C";
                    if (defaultCats.TryGetValue("presentations", out var def))
                        catPresentations.Extensions = [.. def.Extensions];
                }
            });
        }

        // 7. Áudio (Categoria audio)
        if (catAudio != null)
        {
            entries.Add(new UnifiedTypeEntry
            {
                Id = "audio",
                Name = strings.CategoryAudio,
                BadgeText = strings.BadgeAudio,
                Glyph = "\uEC4F",
                Category = catAudio,
                DefaultColorHex = "#8B5CF6",
                GetCurrentColor = () => catAudio.ColorHex,
                SetColor = hex => catAudio.ColorHex = hex,
                Reset = () =>
                {
                    catAudio.ColorHex = "#8B5CF6";
                    if (defaultCats.TryGetValue("audio", out var def))
                        catAudio.Extensions = [.. def.Extensions];
                }
            });
        }

        // 8. Vídeos (Categoria video)
        if (catVideo != null)
        {
            entries.Add(new UnifiedTypeEntry
            {
                Id = "video",
                Name = strings.CategoryVideo,
                BadgeText = strings.BadgeVideo,
                Glyph = "\uE714",
                Category = catVideo,
                DefaultColorHex = "#DC2626",
                GetCurrentColor = () => catVideo.ColorHex,
                SetColor = hex => catVideo.ColorHex = hex,
                Reset = () =>
                {
                    catVideo.ColorHex = "#DC2626";
                    if (defaultCats.TryGetValue("video", out var def))
                        catVideo.Extensions = [.. def.Extensions];
                }
            });
        }

        // 9. Compactados (Categoria archives)
        if (catArchives != null)
        {
            entries.Add(new UnifiedTypeEntry
            {
                Id = "archives",
                Name = strings.CategoryArchives,
                BadgeText = strings.BadgeArchive,
                Glyph = "\uF012",
                Category = catArchives,
                DefaultColorHex = "#B45309",
                GetCurrentColor = () => catArchives.ColorHex,
                SetColor = hex => catArchives.ColorHex = hex,
                Reset = () =>
                {
                    catArchives.ColorHex = "#B45309";
                    if (defaultCats.TryGetValue("archives", out var def))
                        catArchives.Extensions = [.. def.Extensions];
                }
            });
        }

        // 10. Outros Arquivos e Pastas (Fallback geral)
        entries.Add(new UnifiedTypeEntry
        {
            Id = "file",
            Name = strings.UnifiedTypeOtherFilesName,
            BadgeText = strings.KindFile,
            Glyph = "\uE8B7",
            DefaultColorHex = "#D97706",
            SubtitleHint = strings.UnifiedTypeOtherFilesHint,
            GetCurrentColor = () => _settings.ItemColors.GetCustomColor(ItemKind.File) ?? "#D97706",
            SetColor = hex => _settings.ItemColors.SetCustomColor(ItemKind.File, hex),
            Reset = () => _settings.ItemColors.Reset(ItemKind.File)
        });

        // 11. Textos Simples (ItemKind.Text)
        entries.Add(new UnifiedTypeEntry
        {
            Id = "text",
            Name = strings.UnifiedTypePlainTextName,
            BadgeText = strings.KindText,
            Glyph = "\uE8D2",
            DefaultColorHex = "#475569",
            SubtitleHint = strings.SettingsTextHint,
            GetCurrentColor = () => _settings.ItemColors.GetCustomColor(ItemKind.Text) ?? "#475569",
            SetColor = hex => _settings.ItemColors.SetCustomColor(ItemKind.Text, hex),
            Reset = () => _settings.ItemColors.Reset(ItemKind.Text)
        });

        // 12. Caracteres / Emojis (ItemKind.Character)
        entries.Add(new UnifiedTypeEntry
        {
            Id = "character",
            Name = strings.UnifiedTypeCharacterName,
            BadgeText = strings.BadgeCharacter,
            Glyph = "\uE76E",
            DefaultColorHex = "#E11D48",
            SubtitleHint = strings.UnifiedTypeCharacterHint,
            GetCurrentColor = () => _settings.ItemColors.GetCustomColor(ItemKind.Character) ?? "#E11D48",
            SetColor = hex => _settings.ItemColors.SetCustomColor(ItemKind.Character, hex),
            Reset = () => _settings.ItemColors.Reset(ItemKind.Character)
        });

        // 13. Cores (Color) (ItemKind.Color)
        entries.Add(new UnifiedTypeEntry
        {
            Id = "color",
            Name = strings.UnifiedTypeColorName,
            BadgeText = strings.BadgeColor,
            Glyph = "\uE790",
            DefaultColorHex = "#C026D3",
            SubtitleHint = strings.UnifiedTypeColorHint,
            GetCurrentColor = () => _settings.ItemColors.GetCustomColor(ItemKind.Color) ?? "#C026D3",
            SetColor = hex => _settings.ItemColors.SetCustomColor(ItemKind.Color, hex),
            Reset = () => _settings.ItemColors.Reset(ItemKind.Color)
        });

        // 14. Categorias customizadas criadas pelo usuário
        foreach (var customCat in _settings.FileCategories.Categories.Where(c => !c.IsBuiltIn).ToList())
        {
            entries.Add(new UnifiedTypeEntry
            {
                Id = customCat.Id,
                Name = customCat.Name,
                BadgeText = customCat.Name,
                Glyph = "\uED43",
                Category = customCat,
                DefaultColorHex = customCat.ColorHex,
                GetCurrentColor = () => customCat.ColorHex,
                SetColor = hex => customCat.ColorHex = hex,
                Delete = () =>
                {
                    _settings.FileCategories.RemoveCategory(customCat.Id);
                    PopulateUnifiedColorControls();
                    _onSettingsLiveUpdated?.Invoke();
                }
            });
        }

        foreach (var entry in entries)
        {
            var card = CreateUnifiedTypeCard(entry);
            FileCategoriesPanel.Children.Add(card);
        }
    }

    private Border CreateUnifiedTypeCard(UnifiedTypeEntry entry)
    {
        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 10)
        };
        card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

        var rootStack = new StackPanel();

        // Row 1: Header (Left: Icon + Name + Preview Badge; Right: Swatch + HexBox + PickColor Button + Action Button)
        var row1 = new Grid();
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left Area: Category Icon & Name + Preview Badge
        var titleStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var iconBlock = new TextBlock
        {
            Text = entry.Glyph,
            FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 16,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBlock.SetResourceReference(TextBlock.ForegroundProperty, "CardSubtitleBrush");

        var nameBlock = new TextBlock
        {
            Text = entry.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        nameBlock.SetResourceReference(TextBlock.ForegroundProperty, "CardTitleBrush");

        titleStack.Children.Add(iconBlock);
        titleStack.Children.Add(nameBlock);

        // Preview Badge
        var previewBadge = new Border
        {
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 3, 8, 3),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        var badgeText = new TextBlock { Text = entry.BadgeText, FontSize = 11, FontWeight = FontWeights.SemiBold };
        previewBadge.Child = badgeText;
        titleStack.Children.Add(previewBadge);

        Grid.SetColumn(titleStack, 0);
        row1.Children.Add(titleStack);

        // Right Area: Action Controls (Swatch, HexBox, Pick Button, Reset/Delete Button)
        var actionsStack = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var currentColor = entry.GetCurrentColor();

        // Color Swatch (Interactive preview button)
        var colorSwatch = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        actionsStack.Children.Add(colorSwatch);

        // Hex Box
        var hexBox = new TextBox
        {
            Style = (Style)FindResource("FluentTextBox"),
            Width = 78,
            MaxLength = 7,
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Segoe UI Variable Text, Segoe UI"),
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Text = currentColor
        };
        actionsStack.Children.Add(hexBox);

        UpdateCategoryBadge(currentColor, colorSwatch, previewBadge);

        void OpenColorPicker()
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
            try { dialog.Color = System.Drawing.ColorTranslator.FromHtml(entry.GetCurrentColor()); } catch { }
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var chosen = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                hexBox.Text = chosen;
                entry.SetColor(chosen);
                UpdateCategoryBadge(chosen, colorSwatch, previewBadge);
                ApplyTheme(_currentScheme);
                _onSettingsLiveUpdated?.Invoke();
            }
        }

        colorSwatch.MouseLeftButtonUp += (_, _) => OpenColorPicker();

        // Pick Color Button
        var pickBtn = new Button
        {
            Style = (Style)FindResource("FluentButton"),
            Content = LocalizationManager.Strings.SettingsChooseColorButton,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        pickBtn.Click += (_, _) => OpenColorPicker();
        actionsStack.Children.Add(pickBtn);

        // Action Button: Delete if custom, Reset if built-in
        var actionBtn = new Button
        {
            Style = (Style)FindResource("FluentButton"),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (entry.Delete != null)
        {
            actionBtn.Content = LocalizationManager.Strings.SettingsDeleteButton;
            actionBtn.Click += (_, _) => entry.Delete();
        }
        else
        {
            actionBtn.Content = LocalizationManager.Strings.SettingsResetButton;
            actionBtn.Click += (_, _) =>
            {
                entry.Reset?.Invoke();
                var freshColor = entry.GetCurrentColor();
                hexBox.Text = freshColor;
                UpdateCategoryBadge(freshColor, colorSwatch, previewBadge);
                PopulateUnifiedColorControls();
                ApplyTheme(_currentScheme);
                _onSettingsLiveUpdated?.Invoke();
            };
        }
        actionsStack.Children.Add(actionBtn);

        Grid.SetColumn(actionsStack, 1);
        row1.Children.Add(actionsStack);

        hexBox.TextChanged += (_, _) =>
        {
            if (_loading) return;
            var normalized = ItemColorSettings.NormalizeHex(hexBox.Text);
            if (normalized != null)
            {
                entry.SetColor(normalized);
                UpdateCategoryBadge(normalized, colorSwatch, previewBadge);
                ApplyTheme(_currentScheme);
                _onSettingsLiveUpdated?.Invoke();
            }
        };

        rootStack.Children.Add(row1);

        // Row 2: Extensions editor OR Subtitle Hint
        if (entry.HasExtensions && entry.Category != null)
        {
            var row2 = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            var extLabel = new TextBlock
            {
                Text = LocalizationManager.Strings.SettingsExtensionsLabel,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 3)
            };
            extLabel.SetResourceReference(TextBlock.ForegroundProperty, "CardSubtitleBrush");

            var extBox = new TextBox
            {
                Style = (Style)FindResource("FluentTextBox"),
                Text = string.Join(", ", entry.Category.Extensions)
            };
            extBox.TextChanged += (_, _) =>
            {
                if (_loading) return;
                var raw = extBox.Text;
                entry.Category.Extensions = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(FileCategory.NormalizeExtension)
                    .Where(e => !string.IsNullOrEmpty(e))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                _onSettingsLiveUpdated?.Invoke();
            };

            row2.Children.Add(extLabel);
            row2.Children.Add(extBox);
            rootStack.Children.Add(row2);
        }
        else if (!string.IsNullOrEmpty(entry.SubtitleHint))
        {
            var hintText = new TextBlock
            {
                Text = entry.SubtitleHint,
                FontSize = 11,
                Margin = new Thickness(0, 6, 0, 0)
            };
            hintText.SetResourceReference(TextBlock.ForegroundProperty, "CardSubtitleBrush");

            rootStack.Children.Add(hintText);
        }

        card.Child = rootStack;
        return card;
    }

    private void UpdateCategoryBadge(string hex, Border swatch, Border badge)
    {
        try
        {
            var accentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var accentBrush = new SolidColorBrush(accentColor);
            accentBrush.Freeze();

            swatch.Background = accentBrush;
            swatch.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            byte alpha = _currentScheme == ColorScheme.Light ? (byte)0x1F : (byte)0x26;
            var bgColor = System.Windows.Media.Color.FromArgb(alpha, accentColor.R, accentColor.G, accentColor.B);
            var bgBrush = new SolidColorBrush(bgColor);
            bgBrush.Freeze();

            badge.BorderBrush = accentBrush;
            badge.Background = bgBrush;
            if (badge.Child is TextBlock tb)
            {
                tb.Foreground = accentBrush;
            }
        }
        catch
        {
        }
    }

    private void OnResetAllColorsClicked(object sender, RoutedEventArgs e)
    {
        _settings.ItemColors.ResetAll();
        _settings.FileCategories.ResetToDefaults();
        PopulateUnifiedColorControls();
        ApplyTheme(_currentScheme);
        _onSettingsLiveUpdated?.Invoke();
    }

    private void OnAddCategoryClicked(object sender, RoutedEventArgs e)
    {
        NewCategoryPanel.Visibility = Visibility.Visible;
        NewCategoryNameBox.Text = "";
        NewCategoryExtensionsBox.Text = "";
        NewCategoryHexBox.Text = "#9B59B6";
        NewCategoryNameBox.Focus();
    }

    private void OnCancelNewCategoryClicked(object sender, RoutedEventArgs e)
    {
        NewCategoryPanel.Visibility = Visibility.Collapsed;
    }

    private void OnPickNewCategoryColorClicked(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
        try { dialog.Color = System.Drawing.ColorTranslator.FromHtml(NewCategoryHexBox.Text); } catch { }
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            NewCategoryHexBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void OnSaveNewCategoryClicked(object sender, RoutedEventArgs e)
    {
        var name = NewCategoryNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            NewCategoryNameBox.Focus();
            return;
        }

        var color = ItemColorSettings.NormalizeHex(NewCategoryHexBox.Text) ?? "#9B59B6";
        var rawExts = NewCategoryExtensionsBox.Text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        _settings.FileCategories.AddCategory(name, color, rawExts);
        NewCategoryPanel.Visibility = Visibility.Collapsed;
        PopulateUnifiedColorControls();
        _onSettingsLiveUpdated?.Invoke();
    }

    // --- SHORTCUTS ---
    // Two ways to change a global chord: type it and click Apply, or click
    // Record, press the combination and release every key. Both end in
    // Remap, and the status box always says whether it was saved.

    private void OnApplyOpenGesture(object sender, RoutedEventArgs e) =>
        ApplyTypedGesture(HotkeySlot.Open, OpenGestureBox.Text);

    private void OnApplyIncognitoGesture(object sender, RoutedEventArgs e) =>
        ApplyTypedGesture(HotkeySlot.Incognito, IncognitoGestureBox.Text);

    private void ApplyTypedGesture(HotkeySlot slot, string gesture)
    {
        StopRecording();
        if (!HotkeyChord.TryParse(gesture, out var chord) || chord is null)
        {
            ShowNotSaved(slot, LocalizationManager.Strings.SettingsShortcutErrorUnrecognized(gesture.Trim()));
            return;
        }
        Remap(slot, chord);
    }

    private void Remap(HotkeySlot slot, HotkeyChord chord)
    {
        var strings = LocalizationManager.Strings;
        if (_hotkeys is null || _hotkeyHwnd == IntPtr.Zero)
        {
            ShowHotkeyStatus(HotkeyStatusKind.Error, strings.SettingsShortcutStatusNotSavedTitle,
                strings.SettingsShortcutStatusUnavailable);
            return;
        }
        if (chord.FindProblem() is HotkeyProblem problem)
        {
            ShowNotSaved(slot, ProblemText(problem));
            return;
        }
        var outcome = _hotkeys.Remap(_hotkeyHwnd, slot, chord);
        if (!outcome.Registered)
        {
            ShowNotSaved(slot, outcome.Occupied
                ? strings.SettingsShortcutErrorOccupied(chord.ToString())
                : strings.SettingsShortcutStatusFailed);
            return;
        }
        OpenGestureBox.Text = _settings.Shortcuts.OpenGesture;
        IncognitoGestureBox.Text = _settings.Shortcuts.IncognitoGesture;
        ShowHotkeyStatus(HotkeyStatusKind.Success, strings.SettingsShortcutStatusSavedTitle,
            strings.SettingsShortcutStatusSavedBody(SlotName(slot), chord.ToString()));
    }

    private void OnRecordGestureClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !Enum.TryParse<HotkeySlot>(button.Tag as string, out var slot))
        {
            return;
        }
        if (_recordingButton == button)
        {
            // Second click on the same button cancels.
            CancelRecording();
            return;
        }
        StartRecording(slot, button);
    }

    private void StartRecording(HotkeySlot slot, Button button)
    {
        var strings = LocalizationManager.Strings;
        StopRecording();
        if (_hotkeys is null || _hotkeyHwnd == IntPtr.Zero)
        {
            ShowHotkeyStatus(HotkeyStatusKind.Error, strings.SettingsShortcutStatusNotSavedTitle,
                strings.SettingsShortcutStatusUnavailable);
            return;
        }
        // Free the active chords so pressing one reaches this window
        // instead of opening the popup; StopRecording registers them again.
        _hotkeys.UnregisterAll(_hotkeyHwnd);
        _recorder.Start();
        _recordingSlot = slot;
        _recordingButton = button;
        _recordButtonContent = button.Content;
        button.SetResourceReference(Control.BackgroundProperty, "StatusListeningBackgroundBrush");
        button.SetResourceReference(Control.BorderBrushProperty, "StatusListeningBorderBrush");
        ShowRecordingPreview(string.Empty);
        button.Focus();
        ShowHotkeyStatus(HotkeyStatusKind.Listening, strings.SettingsShortcutStatusListeningTitle(SlotName(slot)),
            strings.SettingsShortcutStatusListeningBody);
    }

    private void ShowRecordingPreview(string preview)
    {
        if (_recordingButton is null)
        {
            return;
        }
        var text = new TextBlock
        {
            Text = preview.Length == 0 ? LocalizationManager.Strings.SettingsShortcutRecordListening : preview,
            FontWeight = FontWeights.SemiBold,
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "StatusListeningForegroundBrush");
        _recordingButton.Content = text;
    }

    // Restores the Record button and re-registers the global chords.
    private void StopRecording()
    {
        if (_recordingButton is null)
        {
            return;
        }
        _recorder.Stop();
        _recordingButton.Content = _recordButtonContent;
        _recordingButton.ClearValue(Control.BackgroundProperty);
        _recordingButton.ClearValue(Control.BorderBrushProperty);
        _recordingButton = null;
        _recordButtonContent = null;
        if (_hotkeys is not null && _hotkeyHwnd != IntPtr.Zero)
        {
            _hotkeys.RegisterAll(_hotkeyHwnd);
        }
    }

    private void CancelRecording()
    {
        if (_recordingButton is null)
        {
            return;
        }
        StopRecording();
        ShowHotkeyStatus(HotkeyStatusKind.Info, LocalizationManager.Strings.SettingsShortcutStatusCanceledTitle,
            LocalizationManager.Strings.SettingsShortcutStatusKeepsPrevious(ActiveGesture(_recordingSlot)));
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_recordingButton is null)
        {
            base.OnPreviewKeyDown(e);
            return;
        }
        // Every key belongs to the recorder: no Tab focus moves, no Space/Enter clicks.
        e.Handled = true;
        OnRecordStep(_recorder.KeyDown(VirtualKeyOf(e)));
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (_recordingButton is null)
        {
            base.OnPreviewKeyUp(e);
            return;
        }
        e.Handled = true;
        OnRecordStep(_recorder.KeyUp(VirtualKeyOf(e)));
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        // Clicking anywhere but the Record button being used cancels it.
        if (_recordingButton is Button button
            && !(e.OriginalSource is Visual source && (source == button || button.IsAncestorOf(source))))
        {
            CancelRecording();
        }
        base.OnPreviewMouseDown(e);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        CancelRecording();
        base.OnDeactivated(e);
    }

    private void OnRecordStep(HotkeyRecordStep step)
    {
        var strings = LocalizationManager.Strings;
        var slot = _recordingSlot;
        switch (step.Status)
        {
            case HotkeyRecordStatus.Listening:
                ShowRecordingPreview(step.Preview);
                break;
            case HotkeyRecordStatus.ModifiersOnly:
                ShowRecordingPreview(string.Empty);
                ShowHotkeyStatus(HotkeyStatusKind.Listening, strings.SettingsShortcutStatusListeningTitle(SlotName(slot)),
                    strings.SettingsShortcutStatusModifiersOnly(step.Preview));
                break;
            case HotkeyRecordStatus.Canceled:
                CancelRecording();
                break;
            case HotkeyRecordStatus.Unsupported:
                StopRecording();
                ShowNotSaved(slot, strings.SettingsShortcutErrorUnsupportedKey);
                break;
            case HotkeyRecordStatus.Captured when step.Chord is not null:
                StopRecording();
                Remap(slot, step.Chord);
                break;
        }
    }

    private static uint VirtualKeyOf(KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };
        return (uint)KeyInterop.VirtualKeyFromKey(key);
    }

    private void ShowHotkeyInfo() =>
        ShowHotkeyStatus(HotkeyStatusKind.Info, LocalizationManager.Strings.SettingsShortcutStatusInfoTitle,
            LocalizationManager.Strings.SettingsShortcutStatusInfo);

    private void ShowNotSaved(HotkeySlot slot, string reason)
    {
        var strings = LocalizationManager.Strings;
        var body = _hotkeys is null ? reason : $"{reason} {strings.SettingsShortcutStatusKeepsPrevious(ActiveGesture(slot))}";
        ShowHotkeyStatus(HotkeyStatusKind.Error, strings.SettingsShortcutStatusNotSavedTitle, body);
    }

    private void ShowHotkeyStatus(HotkeyStatusKind kind, string title, string body)
    {
        var (background, border, accent, glyph) = kind switch
        {
            HotkeyStatusKind.Listening => ("StatusListeningBackgroundBrush", "StatusListeningBorderBrush", "StatusListeningForegroundBrush", "\uE765"),
            HotkeyStatusKind.Success => ("StatusSuccessBackgroundBrush", "StatusSuccessBorderBrush", "StatusSuccessForegroundBrush", "\uE73E"),
            HotkeyStatusKind.Error => ("StatusErrorBackgroundBrush", "StatusErrorBorderBrush", "StatusErrorForegroundBrush", "\uE783"),
            _ => ("PreviewCodeBackgroundBrush", "PreviewCodeBorderBrush", "CardTitleBrush", "\uE946"),
        };
        HotkeyStatusBox.SetResourceReference(Border.BackgroundProperty, background);
        HotkeyStatusBox.SetResourceReference(Border.BorderBrushProperty, border);
        HotkeyStatusIcon.SetResourceReference(TextBlock.ForegroundProperty, accent);
        HotkeyStatusTitle.SetResourceReference(TextBlock.ForegroundProperty, accent);
        HotkeyStatus.SetResourceReference(TextBlock.ForegroundProperty,
            kind == HotkeyStatusKind.Info ? "CardSubtitleBrush" : "CardTitleBrush");
        HotkeyStatusIcon.Text = glyph;
        HotkeyStatusTitle.Text = title;
        HotkeyStatus.Text = body;
    }

    private string ActiveGesture(HotkeySlot slot)
    {
        if (_hotkeys is not null)
        {
            return (slot == HotkeySlot.Incognito ? _hotkeys.IncognitoChord : _hotkeys.OpenChord).ToString();
        }
        return slot == HotkeySlot.Incognito ? _settings.Shortcuts.IncognitoGesture : _settings.Shortcuts.OpenGesture;
    }

    private static string SlotName(HotkeySlot slot) => slot == HotkeySlot.Incognito
        ? LocalizationManager.Strings.SettingsShortcutIncognitoTitle
        : LocalizationManager.Strings.SettingsShortcutCompactTitle;

    private static string ProblemText(HotkeyProblem problem) => problem switch
    {
        HotkeyProblem.WinKeyReserved => LocalizationManager.Strings.SettingsShortcutErrorWinKeyReserved,
        HotkeyProblem.F12Reserved => LocalizationManager.Strings.SettingsShortcutErrorF12Reserved,
        _ => LocalizationManager.Strings.SettingsShortcutErrorNeedsModifier,
    };

    // --- FOLDERS ---

    private static void OpenInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
            or InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    private void OnShowWelcomeClicked(object sender, RoutedEventArgs e) => _onShowWelcome?.Invoke();

    private void OnOpenData(object sender, RoutedEventArgs e) => OpenInExplorer(AppFolders.DataDir());

    private void OnOpenConfig(object sender, RoutedEventArgs e) => OpenInExplorer(AppFolders.ConfigDir());

    private void OnOpenCache(object sender, RoutedEventArgs e) => OpenInExplorer(AppFolders.CacheDir());

    protected override void OnClosed(EventArgs e)
    {
        // Never leave the global chords unregistered behind a closed window.
        StopRecording();
        base.OnClosed(e);
        _onClosed();
    }
}
