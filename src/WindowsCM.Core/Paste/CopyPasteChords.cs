// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Paste;

// What choosing an item does (Copyous `itemShortcuts` parity, research 01
// §5): plain Enter/Space copies the item back and pastes it into the
// previously focused app; Shift+Enter/Space only copies. Swap inverts both.
public enum ActivationIntent
{
    CopyOnly,
    CopyAndPaste,
}

public static class CopyPasteChords
{
    public static ActivationIntent Resolve(bool shiftHeld, bool swap)
    {
        var paste = !shiftHeld;
        if (swap)
        {
            paste = !paste;
        }
        return paste ? ActivationIntent.CopyAndPaste : ActivationIntent.CopyOnly;
    }
}
