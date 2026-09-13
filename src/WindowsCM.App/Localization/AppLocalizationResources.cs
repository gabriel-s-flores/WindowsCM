// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using System.Windows;
using WindowsCM.Core.Localization;

namespace WindowsCM.App.Localization;

public static class AppLocalizationResources
{
    public static ResourceDictionary BuildResourceDictionary(IAppStrings strings)
    {
        var dict = new ResourceDictionary();
        PopulateResourceDictionary(dict, strings);
        return dict;
    }

    public static void PopulateResourceDictionary(ResourceDictionary dict, IAppStrings strings)
    {
        var properties = typeof(IAppStrings).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (prop.PropertyType == typeof(string))
            {
                var val = prop.GetValue(strings);
                dict[$"Loc_{prop.Name}"] = val;
            }
        }
    }
}
