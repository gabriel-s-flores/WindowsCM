// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Popup;

// Theme + card policy from the prototype verdict (issue 07 → spec):
// winner A (Cards), Copyous density 1:1, Dark default, no vertical dead
// space, rows fill the horizontal width. The WPF layer renders these;
// Core owns the defaults and constants so they are test-pinned.
public enum PopupTheme
{
    Dark,
    Light,
    HighContrast,
}

public enum PopupProfile
{
    Default,
    Compact,
}

public static class PopupCards
{
    // Verdict A density: horizontal 250×170 cards (prototype BuildCard).
    public const double CardWidth = 250;
    public const double CardHeight = 170;

    // Verdict corrections against the losing rows: link-style rows must not
    // leave blank space above/below, and the popup fills horizontal space
    // instead of floating narrow.
    public const bool FullWidthRows = true;
    public const bool NoVerticalDeadSpace = true;

    public const PopupTheme DefaultTheme = PopupTheme.Dark;
    public const PopupProfile FirstRunProfile = PopupProfile.Default;
}

// The nine Copyous tag colors (research 01 parity inventory).
public static class ItemTags
{
    public static IReadOnlyList<string> All { get; } =
    [
        "#3584e4",
        "#2190a4",
        "#3a944a",
        "#c88800",
        "#ed5b00",
        "#e62d42",
        "#d56199",
        "#9c3cbe",
        "#6f8396",
    ];
}
