// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using WindowsCM.Core.Paste;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Stability;

// Seeded fuzzing of the code that runs on untrusted or unpredictable input:
// the settings file (hand-edited, truncated by a crash, from another
// version), monitor layouts (plugged, unplugged, rearranged) and window
// classes. None of it may ever throw — every one of these runs on the UI
// thread or at startup.
[Collection(StabilityCollection.Name)]
public sealed class RobustnessFuzzTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wcm-fuzz-" + Guid.NewGuid().ToString("N"));

    public RobustnessFuzzTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static readonly string[] Poison =
    [
        "null", "\"\"", "\"Floating\"", "99", "-1", "1e308", "-1e308", "true", "[]", "{}",
        "\"\\u0000\"", "[1,2,3]", "{\"left\":\"x\"}", "0.5", "\"NaN\"",
    ];

    [Fact]
    public void SettingsLoad_CorruptedFiles_NeverThrowAndAlwaysClamp()
    {
        var random = new Random(20260925);
        var valid = SettingsStore.Serialize(AppSettings.Default());
        var path = Path.Combine(_dir, "settings.json");

        for (var i = 0; i < 1500; i++)
        {
            var text = Mutate(valid, random);
            File.WriteAllText(path, text);

            var loaded = SettingsStore.Load(path);

            Assert.NotNull(loaded);
            Assert.True(Enum.IsDefined(loaded.Dialog.LargePlacement), text);
            Assert.NotNull(loaded.Dialog.LargeMonitor);
            Assert.True(loaded.Dialog.LargeFreeBoundsHorizontal is null or { IsUsable: true });
            Assert.True(loaded.Dialog.LargeFreeBoundsVertical is null or { IsUsable: true });
            Assert.InRange(loaded.History.MaxItems, SettingLimits.HistoryLengthMin, SettingLimits.HistoryLengthMax);
            ExerciseLikeTheApp(loaded);
        }
    }

    [Theory]
    [InlineData("""{"fileCategories":{"categories":null}}""")]
    [InlineData("""{"fileCategories":{"categories":[null]}}""")]
    [InlineData("""{"fileCategories":{"categories":[{"id":"images","extensions":null}]}}""")]
    [InlineData("""{"fileCategories":{"categories":[{"id":null,"name":null,"colorHex":null,"extensions":[null,".png"]}]}}""")]
    [InlineData("""{"exclusions":{"processes":null},"dialog":{"largeMonitor":null}}""")]
    public void SettingsLoad_NullShapes_KeepTheRestOfTheSettings(string json)
    {
        // One bad value must not cost every other preference: these load
        // (no fallback to defaults) and the app can use them.
        var withPreference = json.Insert(1, "\"history\":{\"maxItems\":42},");
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, withPreference);

        var loaded = SettingsStore.Load(path);

        Assert.Equal(42, loaded.History.MaxItems);
        Assert.False(File.Exists(SettingsStore.CorruptCopyPath(path)));
        ExerciseLikeTheApp(loaded);
    }

    // What App.BuildServices and the popup do with a freshly loaded file.
    private static void ExerciseLikeTheApp(AppSettings settings)
    {
        settings.ToCaptureOptions();
        settings.ToPasteOptions();
        settings.ToSoundOptions();
        settings.ToCopyFeedbackOptions();
        settings.ToLinkPreviewOptions();
        settings.FileCategories.ResolveCategory(@"C:\docs\photo.png");
        settings.Dialog.FreeBoundsFor(settings.Dialog.Orientation);
        settings.DetectProfile();
        SettingsStore.Serialize(settings);
    }

    [Fact]
    public void SettingsLoad_Unparseable_KeepsTheUsersFileAside()
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ \"history\": { \"maxItems\": 50 }, truncated by a cra");

        var loaded = SettingsStore.Load(path);

        Assert.Equal(AppSettings.Default().History.MaxItems, loaded.History.MaxItems);
        Assert.Equal("{ \"history\": { \"maxItems\": 50 }, truncated by a cra",
            File.ReadAllText(SettingsStore.CorruptCopyPath(path)));
    }

    [Fact]
    public void PlaceFree_RandomLayoutsAndRectangles_AlwaysLandOnAMonitor()
    {
        var random = new Random(42);
        for (var i = 0; i < 20_000; i++)
        {
            var areas = RandomMonitors(random);
            var fallback = areas[random.Next(areas.Count)];
            var orientation = random.Next(2) == 0 ? DialogOrientation.Horizontal : DialogOrientation.Vertical;
            var saved = random.Next(10) == 0 ? null : new WindowBounds
            {
                Left = RandomCoordinate(random),
                Top = RandomCoordinate(random),
                Width = RandomCoordinate(random),
                Height = RandomCoordinate(random),
            };

            var (left, top, width, height) = PopupPlacement.PlaceFree(saved, areas, fallback, orientation);

            Assert.True(double.IsFinite(left) && double.IsFinite(top) && double.IsFinite(width) && double.IsFinite(height));
            Assert.True(width > 0 && height > 0);
            Assert.Contains(areas, a =>
                left >= a.Left - 0.001 && top >= a.Top - 0.001
                && left + width <= a.Right + 0.001 && top + height <= a.Bottom + 0.001);
        }
    }

    [Fact]
    public void ResizeHitTestAndWindowClassification_NeverThrow()
    {
        var random = new Random(3);
        for (var i = 0; i < 20_000; i++)
        {
            var window = new WorkArea(RandomCoordinate(random), RandomCoordinate(random),
                RandomCoordinate(random), RandomCoordinate(random));
            ResizeHitTest.EdgeAt(RandomCoordinate(random), RandomCoordinate(random), window, random.NextDouble() * 20 - 2);

            var className = random.Next(5) == 0 ? null : RandomString(random, random.Next(0, 300));
            var role = PasteTargetPolicy.Classify(new IntPtr(random.Next()), className, random.Next(2) == 0);
            PasteTargetPolicy.Resolve(new ForegroundSnapshot(new IntPtr(random.Next()), role),
                new ForegroundSnapshot(new IntPtr(random.Next()), (WindowRole)random.Next(5)));
        }
    }

    private static string Mutate(string json, Random random)
    {
        var builder = new StringBuilder(json);
        switch (random.Next(5))
        {
            case 0:
                // Truncated write (crash mid-save, full disk).
                return json[..random.Next(json.Length)];
            case 1:
                // Random byte flips.
                for (var i = random.Next(1, 6); i > 0; i--)
                {
                    builder[random.Next(builder.Length)] = (char)random.Next(32, 127);
                }
                return builder.ToString();
            default:
                // A random property value replaced by a poison value.
                var colons = Enumerable.Range(0, json.Length).Where(i => json[i] == ':').ToList();
                var at = colons[random.Next(colons.Count)] + 1;
                var end = at;
                var depth = 0;
                while (end < json.Length)
                {
                    var c = json[end];
                    if (c is '{' or '[')
                    {
                        depth++;
                    }
                    else if (c is '}' or ']')
                    {
                        if (depth == 0)
                        {
                            break;
                        }
                        depth--;
                    }
                    else if (c == ',' && depth == 0)
                    {
                        break;
                    }
                    end++;
                }
                return json[..at] + " " + Poison[random.Next(Poison.Length)] + json[end..];
        }
    }

    private static List<WorkArea> RandomMonitors(Random random)
    {
        var areas = new List<WorkArea>();
        var x = random.Next(-4000, 1);
        for (var i = random.Next(1, 5); i > 0; i--)
        {
            var width = random.Next(640, 3840);
            var height = random.Next(480, 2160);
            var y = random.Next(-500, 500);
            areas.Add(new WorkArea(x, y, x + width, y + height));
            x += width;
        }
        return areas;
    }

    private static double RandomCoordinate(Random random) => random.Next(12) switch
    {
        0 => double.NaN,
        1 => double.PositiveInfinity,
        2 => -1e9,
        3 => 1e9,
        4 => 0,
        _ => random.NextDouble() * 12000 - 6000,
    };

    private static string RandomString(Random random, int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = (char)random.Next(0, 0xD7FF);
        }
        return new string(chars);
    }
}
