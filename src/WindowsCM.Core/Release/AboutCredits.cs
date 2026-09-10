// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Release;

// About-page content (ticket 18, spec story 43, grilling 08 Q17).
// LicenseId doubles as the SPDX expression in the csproj (pinned equal
// by AboutCreditsTests); upstream entries credit the lineage; library
// entries list every bundled dependency with its real license. Core
// ships Microsoft.Data.Sqlite today (version mirrors the csproj
// PackageReference); the WPF shell adds the four MIT decisions from
// research 03 §3.2 (tray) and 05 §§1.3/2/3 (QR, link metadata, editor).
public sealed record UpstreamCredit(string Name, string Url, string License, string Role);

public sealed record ThirdPartyLibrary(string Name, string Url, string License, string? Version);

public static class AboutCredits
{
    public const string AppName = "WindowsCM";
    public const string LicenseId = "GPL-3.0-or-later";
    public const string LicenseFileName = "LICENSE";

    public static IReadOnlyList<UpstreamCredit> Upstream { get; } =
    [
        // Upstream LICENSE is GPL v2 with no or-later grant (GitHub
        // detects GPL-2.0): credit the real license, not ours.
        new("Copyous", "https://github.com/boerdereinar/copyous", "GPL-2.0-only",
            "GNOME clipboard manager ported to Windows"),
        new("Pano", "https://github.com/oae/gnome-shell-pano", "GPL-2.0-only",
            "GNOME Shell clipboard manager in Copyous's lineage"),
    ];

    public static IReadOnlyList<ThirdPartyLibrary> Libraries { get; } =
    [
        new("Microsoft.Data.Sqlite", "https://www.nuget.org/packages/Microsoft.Data.Sqlite",
            "Apache-2.0", "8.0.10"),
        new("QRCoder", "https://github.com/Shane32/QRCoder", "MIT", "1.8.0"),
        new("AngleSharp", "https://github.com/AngleSharp/AngleSharp", "MIT", "1.7.2"),
        new("AvalonEdit", "https://github.com/icsharpcode/AvalonEdit", "MIT", "6.3.1"),
        // Version pinned at shell-restore time; research 03 left the
        // current H.NotifyIcon.Wpf version as an install-time choice.
        new("H.NotifyIcon.WPF", "https://github.com/HavenDV/H.NotifyIcon", "MIT", null),
    ];
}
