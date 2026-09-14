// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using WindowsCM.Core.Lifecycle;
using WindowsCM.Core.Lifecycle.Win32;

namespace WindowsCM.App;

// First-run welcome guide: tells a new user the app lives in the tray (and
// how to find the hidden icon), shows their current global shortcuts and
// tours the features. Shown once by App; Settings > About reopens it.
public partial class WelcomeWindow : Window
{
    private readonly Action _openSettings;
    private readonly bool _loading;

    public WelcomeWindow(string openGesture, string incognitoGesture, Action openSettings)
    {
        _loading = true;
        _openSettings = openSettings;
        InitializeComponent();
        OpenGestureText.Text = AsKeys(openGesture);
        IncognitoGestureText.Text = AsKeys(incognitoGesture);
        AutostartCheck.IsChecked = AutostartManager.IsEnabled(new RegistryRunKeyStore());
        _loading = false;
    }

    // "Ctrl+Shift+V" -> "Ctrl + Shift + V" reads better on a key badge.
    private static string AsKeys(string gesture) => string.Join(" + ", gesture.Split('+'));

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

    private void OnOpenSettingsClicked(object sender, RoutedEventArgs e)
    {
        Close();
        _openSettings();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
