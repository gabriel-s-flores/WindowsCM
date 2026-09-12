// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

public sealed class WindowsThemeDetectorTests
{
    private sealed class FakeThemeDetector : IWindowsThemeDetector
    {
        public ColorScheme CurrentScheme { get; set; } = ColorScheme.Dark;
        public event EventHandler<ColorScheme>? ThemeChanged;
        public bool Disposed { get; private set; }

        public ColorScheme DetectSystemScheme() => CurrentScheme;

        public void SimulateSystemThemeChange(ColorScheme newScheme)
        {
            CurrentScheme = newScheme;
            ThemeChanged?.Invoke(this, newScheme);
        }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void Detector_SimulatesSystemLight_ResolvesLightColors()
    {
        using var detector = new FakeThemeDetector { CurrentScheme = ColorScheme.Light };
        var settings = new ThemeSettings { Theme = ThemeChoice.Default, Scheme = ColorScheme.System };

        var effective = settings.ResolveEffective(detector.DetectSystemScheme());

        Assert.Equal(CustomThemeDefaults.BgLight, effective.Bg);
        Assert.Equal(CustomThemeDefaults.FgLight, effective.Fg);
        Assert.Equal(CustomThemeDefaults.CardBgLight, effective.CardBg);
        Assert.Equal(CustomThemeDefaults.SearchBgLight, effective.SearchBg);
    }

    [Fact]
    public void Detector_SimulatesSystemDark_ResolvesDarkColors()
    {
        using var detector = new FakeThemeDetector { CurrentScheme = ColorScheme.Dark };
        var settings = new ThemeSettings { Theme = ThemeChoice.Default, Scheme = ColorScheme.System };

        var effective = settings.ResolveEffective(detector.DetectSystemScheme());

        Assert.Equal(CustomThemeDefaults.BgDark, effective.Bg);
        Assert.Equal(CustomThemeDefaults.FgDark, effective.Fg);
        Assert.Equal(CustomThemeDefaults.CardBgDark, effective.CardBg);
        Assert.Equal(CustomThemeDefaults.SearchBgDark, effective.SearchBg);
    }

    [Fact]
    public void Detector_SimulatesHighContrast_ResolvesHighContrastColors()
    {
        using var detector = new FakeThemeDetector { CurrentScheme = ColorScheme.HighContrast };
        var settings = new ThemeSettings { Theme = ThemeChoice.Default, Scheme = ColorScheme.System };

        var effective = settings.ResolveEffective(detector.DetectSystemScheme());

        Assert.Equal(CustomThemeDefaults.BgHighContrast, effective.Bg);
        Assert.Equal(CustomThemeDefaults.FgHighContrast, effective.Fg);
    }

    [Fact]
    public void Detector_ThemeChangedEvent_FiresWithNewScheme()
    {
        using var detector = new FakeThemeDetector { CurrentScheme = ColorScheme.Dark };
        ColorScheme? received = null;
        detector.ThemeChanged += (_, scheme) => received = scheme;

        detector.SimulateSystemThemeChange(ColorScheme.Light);

        Assert.Equal(ColorScheme.Light, received);
        Assert.Equal(ColorScheme.Light, detector.DetectSystemScheme());
    }

    [Fact]
    public void Override_ExplicitLight_IgnoresSystemDark()
    {
        using var detector = new FakeThemeDetector { CurrentScheme = ColorScheme.Dark };
        var settings = new ThemeSettings { Theme = ThemeChoice.Default, Scheme = ColorScheme.Light };

        var effective = settings.ResolveEffective(detector.DetectSystemScheme());

        Assert.Equal(CustomThemeDefaults.BgLight, effective.Bg);
        Assert.Equal(CustomThemeDefaults.FgLight, effective.Fg);
    }

    [Fact]
    public void Override_ExplicitDark_IgnoresSystemLight()
    {
        using var detector = new FakeThemeDetector { CurrentScheme = ColorScheme.Light };
        var settings = new ThemeSettings { Theme = ThemeChoice.Default, Scheme = ColorScheme.Dark };

        var effective = settings.ResolveEffective(detector.DetectSystemScheme());

        Assert.Equal(CustomThemeDefaults.BgDark, effective.Bg);
        Assert.Equal(CustomThemeDefaults.FgDark, effective.Fg);
    }

    [Fact]
    public void Detector_Dispose_SetsDisposed()
    {
        var detector = new FakeThemeDetector();
        detector.Dispose();

        Assert.True(detector.Disposed);
    }
}
