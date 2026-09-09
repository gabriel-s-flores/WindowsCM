// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsCM.Core.Settings;

// settings.json persistence (GSettings→JSON map): atomic tmp+move with a
// "<file>~" backup on demand (ActionsStore parity). Missing files return
// defaults without touching disk; corrupt files degrade to defaults, never
// to a half-loaded object.
public static class SettingsStore
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load(string path, bool saveDefault = false)
    {
        if (!File.Exists(path))
        {
            var fresh = AppSettings.Default();
            if (saveDefault)
            {
                Save(path, fresh);
            }
            return fresh;
        }
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(path), JsonOptions);
            if (settings is null)
            {
                return AppSettings.Default();
            }
            settings.ClampAll();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            return AppSettings.Default();
        }
    }

    public static void Save(string path, AppSettings settings, bool backup = false)
    {
        settings.ClampAll();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        if (backup && File.Exists(path))
        {
            File.Copy(path, path + "~", overwrite: true);
        }
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    public static string Serialize(AppSettings settings)
    {
        settings.ClampAll();
        return JsonSerializer.Serialize(settings, JsonOptions);
    }

    public static AppSettings Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        if (settings is null)
        {
            return AppSettings.Default();
        }
        settings.ClampAll();
        return settings;
    }
}
