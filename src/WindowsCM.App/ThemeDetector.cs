// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows.Forms;
using Microsoft.Win32;
using WindowsCM.Core.Settings;

namespace WindowsCM.App;

// Production Windows theme detector over registry and SystemEvents.
// Detects Windows 10/11 light or dark system/app theme and high contrast.
public sealed class Win32WindowsThemeDetector : IWindowsThemeDetector
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";
    private bool _disposed;

    public event EventHandler<ColorScheme>? ThemeChanged;

    public Win32WindowsThemeDetector()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public ColorScheme DetectSystemScheme()
    {
        if (SystemInformation.HighContrast)
        {
            return ColorScheme.HighContrast;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (key?.GetValue(AppsUseLightThemeValue) is int lightDword)
            {
                return lightDword == 1 ? ColorScheme.Light : ColorScheme.Dark;
            }
        }
        catch
        {
            // Registry read failures fall back gracefully to dark
        }

        return ColorScheme.Dark;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
        {
            var scheme = DetectSystemScheme();
            ThemeChanged?.Invoke(this, scheme);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
