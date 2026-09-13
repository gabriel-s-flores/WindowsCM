// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Settings;

// First-run state for the welcome guide (tray discovery + feature tour).
// WelcomeShown defaults to true so settings files written before the guide
// existed (no "onboarding" section) keep it and upgrading users are not
// greeted as first-timers; only a brand-new profile starts at false
// (AppSettings.Default, used when there is no settings file yet).
public sealed class OnboardingSettings
{
    public bool WelcomeShown { get; set; } = true;
}
