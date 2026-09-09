// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;

namespace WindowsCM.Core.Actions;

// QR payload policy (Copyous qrcode action parity). The default action
// covers the five text-like kinds on Ctrl+Q; images and file lists have
// no meaningful string payload. Bitmap rendering (QRCoder) belongs to the
// WPF dialog — Core only decides eligibility and hands over the content,
// so this stays UI-free and directly testable.
public static class QrActions
{
    // Copyous default qrcode types: Text, Code, Link, Character, Color.
    public static readonly HashSet<ItemKind> SupportedKinds =
    [
        ItemKind.Text,
        ItemKind.Code,
        ItemKind.Link,
        ItemKind.Character,
        ItemKind.Color,
    ];

    public static bool IsSupported(ItemKind kind) => SupportedKinds.Contains(kind);

    // The dialog payload: the item content verbatim. Null when the kind
    // cannot be encoded (the popup chord must then stay silent).
    public static string? Payload(ItemKind kind, string content) =>
        IsSupported(kind) && content.Length > 0 ? content : null;
}
