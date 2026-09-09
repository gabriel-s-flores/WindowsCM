// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.History;

// The eight Copyous content kinds. Stored as TEXT in SQLite.
public enum ItemKind
{
    Text,
    Code,
    Image,
    // A single dropped/copied file.
    File,
    // Two or more files (contents joined by newline).
    Files,
    Link,
    Character,
    Color,
}
