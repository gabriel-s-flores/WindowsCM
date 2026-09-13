// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

// The welcome guide shows once: on a brand-new profile only, never to
// users upgrading from a build that predates it.
public sealed class OnboardingSettingsTests
{
    private static string TempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");

    private static AppSettings LoadJson(string json)
    {
        var path = TempSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return SettingsStore.Load(path);
    }

    [Fact]
    public void BrandNewProfile_HasNotSeenWelcome()
    {
        Assert.False(AppSettings.Default().Onboarding.WelcomeShown);
    }

    [Fact]
    public void MissingSettingsFile_IsFirstRun()
    {
        Assert.False(SettingsStore.Load(TempSettingsPath()).Onboarding.WelcomeShown);
    }

    [Fact]
    public void SettingsFromBeforeTheGuide_CountAsAlreadyWelcomed()
    {
        var settings = LoadJson("""{ "language": "English", "history": { "maxItems": 50 } }""");

        Assert.True(settings.Onboarding.WelcomeShown);
        Assert.Equal(50, settings.History.MaxItems);
    }

    [Fact]
    public void NullOnboardingSection_IsCoerced()
    {
        var settings = LoadJson("""{ "onboarding": null }""");

        Assert.NotNull(settings.Onboarding);
        Assert.True(settings.Onboarding.WelcomeShown);
    }

    [Fact]
    public void FirstRunState_SurvivesAnEarlySave()
    {
        // Something saving settings before the guide opens must not skip it.
        var path = TempSettingsPath();
        SettingsStore.Save(path, AppSettings.Default());

        Assert.False(SettingsStore.Load(path).Onboarding.WelcomeShown);
    }

    [Fact]
    public void MarkingShown_Persists()
    {
        var path = TempSettingsPath();
        var settings = AppSettings.Default();
        settings.Onboarding.WelcomeShown = true;
        SettingsStore.Save(path, settings);

        Assert.True(SettingsStore.Load(path).Onboarding.WelcomeShown);
    }
}
