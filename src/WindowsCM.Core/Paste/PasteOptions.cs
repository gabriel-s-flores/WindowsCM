// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Paste;

// Which key sequence pastes into the target app. Ctrl+V works in ~every
// Win32/WPF/browser app; Shift+Insert is a manual opt-in for terminals with
// CUA bindings (research 05 §5: no auto-detection, terminal heuristics
// deferred past v1).
public enum PasteSequence
{
    CtrlV,
    ShiftInsert,
}

// Paste tunables (spec: measured tunables, never ported constants).
public sealed class PasteOptions
{
    public PasteSequence PasteSequence { get; set; } = PasteSequence.CtrlV;

    // Post-focus delay before injection (research 02: no Windows-prescribed
    // value; 100–300ms tunable, never a real sleep in tests).
    public int PasteDelayMs { get; set; } = 200;

    // Copyous `swap-copy-shortcut` parity: inverts the Enter/Space
    // copy-vs-paste chords end to end.
    public bool SwapCopyPaste { get; set; } = false;
}
