// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Hotkeys;

namespace WindowsCM.Core.Tests.Hotkeys;

// Unshifted character per virtual key, like ToUnicodeEx on one layout.
// Lookups by character are case-insensitive, like VkKeyScanEx with Shift.
public sealed class FakeKeyboardLayout : IKeyboardLayout
{
    private readonly Dictionary<uint, char> _charByVk;

    public FakeKeyboardLayout(Dictionary<uint, char> charByVk)
    {
        _charByVk = charByVk;
    }

    // Brazilian ABNT2: Ç on OEM_1, dead ´ on OEM_4, / on ABNT_C1.
    public static FakeKeyboardLayout Abnt2() => new(new()
    {
        [0xBA] = 'ç',
        [0xDB] = '´',
        [0xC1] = '/',
        [0xBB] = '=',
        [0xC0] = '\'',
        ['A'] = 'a',
        ['V'] = 'v',
        ['2'] = '2',
    });

    // Russian ЙЦУКЕН: letter keys type Cyrillic, OEM_1 types ж.
    public static FakeKeyboardLayout Russian() => new(new()
    {
        ['F'] = 'а',
        ['R'] = 'к',
        ['K'] = 'л',
        ['C'] = 'с',
        ['Q'] = 'й',
        ['L'] = 'д',
        ['B'] = 'и',
        ['J'] = 'о',
        ['I'] = 'ш',
        ['X'] = 'ч',
        [0xBA] = 'ж',
        [0xDB] = 'х',
    });

    // German QWERTZ: + on OEM_PLUS, ß on OEM_4.
    public static FakeKeyboardLayout German() => new(new()
    {
        [0xBB] = '+',
        [0xDB] = 'ß',
        [0xBA] = 'ü',
    });

    public uint? VirtualKeyForChar(char c)
    {
        foreach (var (vk, typed) in _charByVk)
        {
            if (char.ToLowerInvariant(typed) == char.ToLowerInvariant(c))
            {
                return vk;
            }
        }
        return null;
    }

    public char? CharForVirtualKey(uint virtualKey) =>
        _charByVk.TryGetValue(virtualKey, out var c) ? c : null;
}
