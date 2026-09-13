// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;

namespace WindowsCM.Core.Localization;

public static class LocalizationManager
{
    private static AppLanguage _currentLanguage = AppLanguage.System;

    public static event Action? LanguageChanged;

    public static AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                LanguageChanged?.Invoke();
            }
        }
    }

    public static bool IsPortuguese => ResolveIsPortuguese(_currentLanguage);

    public static IAppStrings Strings => IsPortuguese
        ? PortugueseAppStrings.Instance
        : EnglishAppStrings.Instance;

    public static bool ResolveIsPortuguese(AppLanguage language, CultureInfo? systemCulture = null)
    {
        if (language == AppLanguage.Portuguese)
        {
            return true;
        }
        if (language == AppLanguage.English)
        {
            return false;
        }

        var culture = systemCulture ?? CultureInfo.CurrentUICulture;
        return culture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
    }

    public static void SetLanguage(AppLanguage language)
    {
        CurrentLanguage = language;
    }
}
