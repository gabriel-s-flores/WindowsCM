// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Hotkeys;

// Register/unregister against one HWND. Production P/Invokes user32
// (Win32HotkeyRegistrar); tests fake it, asserting mods+vk and error codes.
public interface IHotkeyRegistrar
{
    bool TryRegister(IntPtr hwnd, int id, uint modifiers, uint vk, out int error);
    void Unregister(IntPtr hwnd, int id);
}

// Gesture persistence behind remaps. Production stores the gesture strings
// in the local JSON settings (GSettings→JSON map); tests use memory.
public interface IHotkeySettings
{
    string OpenGesture { get; set; }
    string IncognitoGesture { get; set; }
    void Save();
}

public sealed class MemoryHotkeySettings : IHotkeySettings
{
    public string OpenGesture { get; set; } = HotkeyDefaults.OpenGesture;
    public string IncognitoGesture { get; set; } = HotkeyDefaults.IncognitoGesture;
    public int Saves { get; private set; }
    public void Save() => Saves++;
}
