// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Windows;
using WindowsCM.Core.Hotkeys;
using WindowsCM.Core.Lifecycle;
using WindowsCM.Core.Lifecycle.Win32;
using WindowsCM.Core.Release;
using WindowsCM.Core.Settings;

namespace WindowsCM.App;

// v1 Settings window (ticket 19 scope): hotkey remap with live guidance,
// autostart toggle, folder shortcuts, versions, credits. The full
// per-screen settings UI is ticket 20; edits here persist through the
// shared AppSettings object (saved on close by App).
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly HotkeyService? _hotkeys;
    private readonly IntPtr _hotkeyHwnd;
    private readonly Action _onClosed;
    private bool _loading = true;

    public SettingsWindow(
        AppSettings settings,
        string settingsPath,
        HotkeyService? hotkeys,
        IntPtr hotkeyHwnd,
        Action onClosed)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        _hotkeyHwnd = hotkeyHwnd;
        _onClosed = onClosed;
        InitializeComponent();

        OpenGestureBox.Text = settings.Shortcuts.OpenGesture;
        IncognitoGestureBox.Text = settings.Shortcuts.IncognitoGesture;
        HotkeyStatus.Text = "Padrões: abrir com Ctrl+Shift+V, anônimo com Ctrl+Shift+Alt+V. " +
            "Combinações com a tecla Win são reservadas pelo sistema operacional.";

        AutostartCheck.IsChecked = AutostartManager.IsEnabled(new RegistryRunKeyStore());

        var info = DiagnosticsInfo.Collect(settings, settingsPath);
        TrayGuidanceText.Text = info.TrayGuidance;
        PathsText.Text = $"Dados: {info.DataDir}\nConfigurações: {info.ConfigDir}\nCache: {info.CacheDir}\n" +
            $"Banco de dados: {info.DatabasePath}\nAções: {info.ActionsPath}\nConfiguração: {info.SettingsPath}";
        VersionsText.Text = $"WindowsCM {info.AppVersion} ({AboutCredits.LicenseId})\n" +
            $".NET {info.DotNetVersion}\nCliente SQLite {info.SqliteVersion}";
        CreditsText.Text = string.Join("\n", AboutCredits.Upstream
                .Select(u => $"{u.Name} ({u.License}) — {u.Role}"))
            + "\n" + string.Join("\n", AboutCredits.Libraries
                .Select(l => $"{l.Name} ({l.License}){(l.Version is null ? "" : " " + l.Version)}"));
        _loading = false;
    }

    private void OnApplyOpenGesture(object sender, RoutedEventArgs e) =>
        Remap(HotkeySlot.Open, OpenGestureBox.Text);

    private void OnApplyIncognitoGesture(object sender, RoutedEventArgs e) =>
        Remap(HotkeySlot.Incognito, IncognitoGestureBox.Text);

    private void Remap(HotkeySlot slot, string gesture)
    {
        if (_hotkeys is null || _hotkeyHwnd == IntPtr.Zero)
        {
            HotkeyStatus.Text = "Atalhos indisponíveis nesta sessão.";
            return;
        }
        var error = ShortcutSettings.ValidateGlobalGesture(gesture);
        if (error is not null)
        {
            HotkeyStatus.Text = error;
            return;
        }
        var outcome = _hotkeys.Remap(_hotkeyHwnd, slot, HotkeyChord.Parse(gesture));
        HotkeyStatus.Text = outcome.Registered
            ? $"Atalho registrado: {gesture}."
            : outcome.Diagnostics ?? "Falha ao remapear atalho.";
        if (outcome.Registered)
        {
            OpenGestureBox.Text = _settings.Shortcuts.OpenGesture;
            IncognitoGestureBox.Text = _settings.Shortcuts.IncognitoGesture;
        }
    }

    private void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        var store = new RegistryRunKeyStore();
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            return;
        }
        if (AutostartCheck.IsChecked == true)
        {
            AutostartManager.Enable(store, exe);
        }
        else
        {
            AutostartManager.Disable(store);
        }
    }

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

    private void OnOpenData(object sender, RoutedEventArgs e) => OpenInExplorer(AppFolders.DataDir());

    private void OnOpenConfig(object sender, RoutedEventArgs e) => OpenInExplorer(AppFolders.ConfigDir());

    private void OnOpenCache(object sender, RoutedEventArgs e) => OpenInExplorer(AppFolders.CacheDir());

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _onClosed();
    }
}
