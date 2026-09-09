// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Hotkeys;

// Win32 modifier flags (research 03 §1.3). Kept as the raw fsModifiers
// values so the service can OR them straight into RegisterHotKey.
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}
