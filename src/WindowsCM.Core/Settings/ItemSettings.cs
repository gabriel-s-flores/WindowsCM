// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Settings;

// Item screen (Copyous Item parity): card size, dynamic height (vertical
// orientation only — the settings window gates it like the original) and
// tab width.
public sealed class ItemSettings
{
    public int Width { get; set; } = SettingLimits.ItemWidthDefault;
    public int Height { get; set; } = SettingLimits.ItemHeightDefault;
    public bool DynamicHeight { get; set; } = false;
    public int TabWidth { get; set; } = SettingLimits.TabWidthDefault;

    public void Clamp()
    {
        Width = SettingLimits.ClampInt(Width, SettingLimits.ItemWidthMin, SettingLimits.ItemWidthMax);
        Height = SettingLimits.ClampInt(Height, SettingLimits.ItemHeightMin, SettingLimits.ItemHeightMax);
        TabWidth = SettingLimits.ClampInt(TabWidth, SettingLimits.TabWidthMin, SettingLimits.TabWidthMax);
    }

    // Dynamic height only takes effect in vertical orientation
    // (itemCustomization.ts gating parity).
    public bool DynamicApplies(DialogOrientation orientation) =>
        DynamicHeight && orientation == DialogOrientation.Vertical;
}

public enum HeaderControlsVisibility
{
    Visible = 0,
    VisibleOnHover = 1,
    Hidden = 2,
}

// Header screen (Copyous Header parity).
public sealed class HeaderSettings
{
    public bool ShowHeader { get; set; } = true;
    public HeaderControlsVisibility Controls { get; set; } = HeaderControlsVisibility.Visible;
    public bool ShowItemTitle { get; set; } = true;
}
