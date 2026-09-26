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

// How the large window is placed (ADR 0006). The two docked modes keep
// the edge anchoring (LargeHorizontalPosition / LargeVerticalPosition).
public enum LargePlacementMode
{
    // Docked to an edge of the monitor under the mouse (original behavior).
    FollowMouse = 0,
    // Docked to an edge of the monitor the user picked (LargeMonitor).
    FixedMonitor = 1,
    // Wherever the user dragged it, at the size they left it.
    Free = 2,
}

// A window rectangle in WPF device-independent units (virtual-screen
// coordinates, the same space Window.Left/Top use).
public sealed class WindowBounds
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public bool IsUsable =>
        double.IsFinite(Left) && double.IsFinite(Top)
        && double.IsFinite(Width) && double.IsFinite(Height)
        && Width > 0 && Height > 0;
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
    public LargePlacementMode LargePlacement { get; set; } = LargePlacementMode.FollowMouse;

    // Screen.DeviceName of the monitor picked for FixedMonitor (e.g.
    // \\.\DISPLAY2); empty means the primary monitor. Kept even while that
    // monitor is disconnected, so it is used again once it comes back.
    public string LargeMonitor { get; set; } = "";

    // Free-mode rectangles, one per orientation: a wide strip and a tall
    // column are different shapes, so switching orientation never squeezes
    // one into the other. Null until the user first places the window.
    public WindowBounds? LargeFreeBoundsHorizontal { get; set; }
    public WindowBounds? LargeFreeBoundsVertical { get; set; }

    public WindowBounds? FreeBoundsFor(DialogOrientation orientation) =>
        orientation == DialogOrientation.Vertical ? LargeFreeBoundsVertical : LargeFreeBoundsHorizontal;

    public void SetFreeBounds(DialogOrientation orientation, WindowBounds? bounds)
    {
        if (orientation == DialogOrientation.Vertical)
        {
            LargeFreeBoundsVertical = bounds;
        }
        else
        {
            LargeFreeBoundsHorizontal = bounds;
        }
    }

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
        if (!Enum.IsDefined(typeof(LargePlacementMode), LargePlacement))
            LargePlacement = LargePlacementMode.FollowMouse;
        LargeMonitor ??= "";
        if (LargeFreeBoundsHorizontal is { IsUsable: false })
            LargeFreeBoundsHorizontal = null;
        if (LargeFreeBoundsVertical is { IsUsable: false })
            LargeFreeBoundsVertical = null;

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
