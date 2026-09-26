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
                ReadWithRetry(path), JsonOptions);
            if (settings is null)
            {
                return AppSettings.Default();
            }
            settings.ClampAll();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked or permission-denied: never block startup on settings.
            return AppSettings.Default();
        }
        catch (Exception)
        {
            // Any unreadable content (malformed JSON, wrong types, values a
            // clamp cannot digest): start from defaults — settings must
            // never keep the app from starting — but keep the user's file
            // aside first, since the next Save would otherwise overwrite
            // every preference without a trace.
            PreserveCorrupt(path);
            return AppSettings.Default();
        }
    }

    // A lock held for a moment at logon (antivirus, a sync client) used to
    // load defaults, and the next save wrote them over every preference.
    // Brief waits are fine here: settings load once, at startup.
    private static readonly TimeSpan[] ReadRetryDelays =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(400),
        TimeSpan.FromMilliseconds(800),
    ];

    private static string ReadWithRetry(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException) when (attempt < ReadRetryDelays.Length && File.Exists(path))
            {
                Thread.Sleep(ReadRetryDelays[attempt]);
            }
        }
    }

    public static string CorruptCopyPath(string path) => path + ".corrupt";

    private static void PreserveCorrupt(string path)
    {
        try
        {
            File.Copy(path, CorruptCopyPath(path), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the defaults still load.
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
