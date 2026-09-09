// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Settings;

// Default vs Compact preset matrix (profiles.ts:164 literals, research 01
// §5). The settings window offers Default/Compact/Custom: applying a
// preset writes the 13 keys, and Detect reports Custom as soon as any key
// drifts. First run is Default (grilling 06 Q14).
public static class ProfilePresets
{
    public const ProfileName FirstRun = ProfileName.Default;

    public static void Apply(AppSettings settings, ProfileName profile)
    {
        switch (profile)
        {
            case ProfileName.Default:
                ApplyDefault(settings);
                break;
            case ProfileName.Compact:
                ApplyCompact(settings);
                break;
            case ProfileName.Custom:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(profile));
        }
    }

    public static ProfileName Detect(AppSettings settings) =>
        MatchesDefault(settings) ? ProfileName.Default
        : MatchesCompact(settings) ? ProfileName.Compact
        : ProfileName.Custom;

    public static void ApplyDefault(AppSettings s)
    {
        s.Dialog.ShowAtPointer = false;
        s.Dialog.Orientation = DialogOrientation.Horizontal;
        s.Dialog.VerticalPosition = VerticalDock.Top;
        s.Dialog.HorizontalPosition = HorizontalDock.Fill;
        s.Dialog.Size = SettingLimits.ClipboardSizeDefault;
        s.Dialog.AutoHideSearch = false;
        s.Item.Width = SettingLimits.ItemWidthDefault;
        s.Item.Height = SettingLimits.ItemHeightDefault;
        s.Item.DynamicHeight = false;
        s.Header.ShowHeader = true;
        s.Header.Controls = HeaderControlsVisibility.Visible;
        s.PerType.File.Visibility = FilePreviewVisibility.PreviewOrInfo;
        s.PerType.Link.Orientation = DialogOrientation.Vertical;
    }

    public static void ApplyCompact(AppSettings s)
    {
        s.Dialog.ShowAtPointer = true;
        s.Dialog.Orientation = DialogOrientation.Vertical;
        s.Dialog.VerticalPosition = VerticalDock.Fill;
        s.Dialog.HorizontalPosition = HorizontalDock.Left;
        s.Dialog.Size = SettingLimits.ClipboardSizeDefault;
        s.Dialog.AutoHideSearch = true;
        s.Item.Width = 300;
        s.Item.Height = 100;
        s.Item.DynamicHeight = true;
        s.Header.ShowHeader = false;
        s.Header.Controls = HeaderControlsVisibility.VisibleOnHover;
        s.PerType.File.Visibility = FilePreviewVisibility.InfoOnly;
        s.PerType.Link.Orientation = DialogOrientation.Horizontal;
    }

    private static bool MatchesDefault(AppSettings s) =>
        s.Dialog.ShowAtPointer == false
        && s.Dialog.Orientation == DialogOrientation.Horizontal
        && s.Dialog.VerticalPosition == VerticalDock.Top
        && s.Dialog.HorizontalPosition == HorizontalDock.Fill
        && s.Dialog.Size == SettingLimits.ClipboardSizeDefault
        && s.Dialog.AutoHideSearch == false
        && s.Item.Width == SettingLimits.ItemWidthDefault
        && s.Item.Height == SettingLimits.ItemHeightDefault
        && s.Item.DynamicHeight == false
        && s.Header.ShowHeader
        && s.Header.Controls == HeaderControlsVisibility.Visible
        && s.PerType.File.Visibility == FilePreviewVisibility.PreviewOrInfo
        && s.PerType.Link.Orientation == DialogOrientation.Vertical;

    private static bool MatchesCompact(AppSettings s) =>
        s.Dialog.ShowAtPointer
        && s.Dialog.Orientation == DialogOrientation.Vertical
        && s.Dialog.VerticalPosition == VerticalDock.Fill
        && s.Dialog.HorizontalPosition == HorizontalDock.Left
        && s.Dialog.Size == SettingLimits.ClipboardSizeDefault
        && s.Dialog.AutoHideSearch
        && s.Item.Width == 300
        && s.Item.Height == 100
        && s.Item.DynamicHeight
        && !s.Header.ShowHeader
        && s.Header.Controls == HeaderControlsVisibility.VisibleOnHover
        && s.PerType.File.Visibility == FilePreviewVisibility.InfoOnly
        && s.PerType.Link.Orientation == DialogOrientation.Horizontal;
}
