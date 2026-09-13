// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Hotkeys;

// Character <-> virtual-key lookups against the user's keyboard layouts, so
// chords can use keys beyond A-Z/0-9 (Ç on ABNT2, Cyrillic letters, ...).
// Production asks user32 (Win32KeyboardLayout); tests fake it.
public interface IKeyboardLayout
{
    // Virtual key that types `c` without AltGr on an installed layout, or null.
    uint? VirtualKeyForChar(char c);

    // Character the key types unshifted on the active layout, or null.
    char? CharForVirtualKey(uint virtualKey);
}
