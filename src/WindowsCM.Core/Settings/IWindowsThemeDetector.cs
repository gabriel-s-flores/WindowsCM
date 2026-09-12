// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Settings;

public interface IWindowsThemeDetector : IDisposable
{
    ColorScheme DetectSystemScheme();
    event EventHandler<ColorScheme>? ThemeChanged;
}
