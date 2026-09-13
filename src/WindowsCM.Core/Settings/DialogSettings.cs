// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Settings;

public enum DialogOrientation
{
    Horizontal = 0,
    Vertical = 1,
}

public enum VerticalDock
{
    Top = 0,
    Center = 1,
    Bottom = 2,
    Fill = 3,
}

public enum HorizontalDock
{
    Left = 0,
    Center = 1,
    Right = 2,
    Fill = 3,
}

public enum LargeHorizontalPosition
{
    Bottom = 0,
    Top = 1,
}

public enum LargeVerticalPosition
{
    Left = 0,
    Right = 1,
}

public enum HorizontalItemOrder
{
    RecentOnLeft = 0,
    RecentOnRight = 1,
}

public enum VerticalItemOrder
{
    RecentOnTop = 0,
    RecentOnBottom = 1,
}

public enum VerticalScrollbarPosition
{
    Right = 0,
    Left = 1,
}

public enum HorizontalScrollbarPosition
{
    Bottom = 0,
    Top = 1,
}

// Dialog screen (Copyous Dialog parity, 01 §5): orientation, dock,
// size, margins, search auto-hide and scrollbar. First run is the Default
// profile: ShowAtPointer off with the show-at-cursor rule kept (grilling
// 06 Q14). Each axis accepts only its own tokens: a cross-axis token
// ("top" on the horizontal key) means a corrupt import, and silently
// mapping it onto the wrong dock would corrupt direction — so it throws
// like any unknown token instead of aliasing.
public sealed class DialogSettings
{
    public bool ShowAtPointer { get; set; } = false;
    public bool ShowAtCursor { get; set; } = false;
    public DialogOrientation Orientation { get; set; } = DialogOrientation.Horizontal;
    public VerticalDock VerticalPosition { get; set; } = VerticalDock.Top;
    public HorizontalDock HorizontalPosition { get; set; } = HorizontalDock.Fill;
    public int Size { get; set; } = SettingLimits.ClipboardSizeDefault;
    public int MarginTop { get; set; } = SettingLimits.MarginDefault;
    public int MarginRight { get; set; } = SettingLimits.MarginDefault;
    public int MarginBottom { get; set; } = SettingLimits.MarginDefault;
    public int MarginLeft { get; set; } = SettingLimits.MarginDefault;
    public bool AutoHideSearch { get; set; } = false;
    public bool ShowScrollbar { get; set; } = true;

    // Configurações da Área Grande (PopupWindow)
    public LargeHorizontalPosition LargeHorizontalPosition { get; set; } = LargeHorizontalPosition.Bottom;
    public LargeVerticalPosition LargeVerticalPosition { get; set; } = LargeVerticalPosition.Left;
    public HorizontalItemOrder LargeHorizontalOrder { get; set; } = HorizontalItemOrder.RecentOnLeft;
    public VerticalItemOrder LargeVerticalOrder { get; set; } = VerticalItemOrder.RecentOnTop;

    // Posicionamento da barra de scroll (customizável por orientação)
    public VerticalScrollbarPosition VerticalScrollbarPosition { get; set; } = VerticalScrollbarPosition.Right;
    public HorizontalScrollbarPosition HorizontalScrollbarPosition { get; set; } = HorizontalScrollbarPosition.Bottom;

    // Configurações do Menu Compacto (CompactPopupWindow)
    public DialogOrientation CompactOrientation { get; set; } = DialogOrientation.Vertical;
    public VerticalItemOrder CompactVerticalOrder { get; set; } = VerticalItemOrder.RecentOnTop;
    public HorizontalItemOrder CompactHorizontalOrder { get; set; } = HorizontalItemOrder.RecentOnLeft;

    public void Clamp()
    {
        Size = SettingLimits.ClampInt(Size, SettingLimits.ClipboardSizeMin, SettingLimits.ClipboardSizeMax);
        MarginTop = SettingLimits.ClampInt(MarginTop, SettingLimits.MarginMin, SettingLimits.MarginMax);
        MarginRight = SettingLimits.ClampInt(MarginRight, SettingLimits.MarginMin, SettingLimits.MarginMax);
        MarginBottom = SettingLimits.ClampInt(MarginBottom, SettingLimits.MarginMin, SettingLimits.MarginMax);
        MarginLeft = SettingLimits.ClampInt(MarginLeft, SettingLimits.MarginMin, SettingLimits.MarginMax);

        if (!Enum.IsDefined(typeof(LargeHorizontalPosition), LargeHorizontalPosition))
            LargeHorizontalPosition = LargeHorizontalPosition.Bottom;
        if (!Enum.IsDefined(typeof(LargeVerticalPosition), LargeVerticalPosition))
            LargeVerticalPosition = LargeVerticalPosition.Left;
        if (!Enum.IsDefined(typeof(HorizontalItemOrder), LargeHorizontalOrder))
            LargeHorizontalOrder = HorizontalItemOrder.RecentOnLeft;
        if (!Enum.IsDefined(typeof(VerticalItemOrder), LargeVerticalOrder))
            LargeVerticalOrder = VerticalItemOrder.RecentOnTop;

        if (!Enum.IsDefined(typeof(VerticalScrollbarPosition), VerticalScrollbarPosition))
            VerticalScrollbarPosition = VerticalScrollbarPosition.Right;
        if (!Enum.IsDefined(typeof(HorizontalScrollbarPosition), HorizontalScrollbarPosition))
            HorizontalScrollbarPosition = HorizontalScrollbarPosition.Bottom;

        if (!Enum.IsDefined(typeof(DialogOrientation), CompactOrientation))
            CompactOrientation = DialogOrientation.Vertical;
        if (!Enum.IsDefined(typeof(VerticalItemOrder), CompactVerticalOrder))
            CompactVerticalOrder = VerticalItemOrder.RecentOnTop;
        if (!Enum.IsDefined(typeof(HorizontalItemOrder), CompactHorizontalOrder))
            CompactHorizontalOrder = HorizontalItemOrder.RecentOnLeft;
    }

    public static VerticalDock NormalizeVerticalToken(string token) =>
        token.Trim().ToLowerInvariant() switch
        {
            "top" => VerticalDock.Top,
            "center" => VerticalDock.Center,
            "bottom" => VerticalDock.Bottom,
            "fill" => VerticalDock.Fill,
            _ => throw new FormatException($"Unknown vertical position '{token}'."),
        };

    public static HorizontalDock NormalizeHorizontalToken(string token) =>
        token.Trim().ToLowerInvariant() switch
        {
            "left" => HorizontalDock.Left,
            "center" => HorizontalDock.Center,
            "right" => HorizontalDock.Right,
            "fill" => HorizontalDock.Fill,
            _ => throw new FormatException($"Unknown horizontal position '{token}'."),
        };
}
