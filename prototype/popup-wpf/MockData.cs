// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace PopupProto
{
    // PROTOTYPE — throwaway mock data. In-memory only: 8 items, one per
    // Copyous type (Text, Code, Image, File, Files, Link, Character, Color),
    // tags spread over 8 of the 9 Copyous tag colors.
    public enum ItemKind { Text, Code, Image, File, Files, Link, Character, Color }

    public class MockItem
    {
        public ItemKind Kind;
        public string Title = "";
        public string Meta = "";
        public string Body = "";
        public string TagHex = "#3584e4";
        public bool Pinned;
        public string DefaultAction = "copy";
    }

    public static class MockData
    {
        public static readonly string[] TagPalette = new string[]
        {
            "#3584e4", "#2190a4", "#3a944a", "#c88800", "#ed5b00",
            "#e62d42", "#d56199", "#9c3cbe", "#6f8396"
        };

        public static List<MockItem> All()
        {
            List<MockItem> items = new List<MockItem>();

            items.Add(new MockItem {
                Kind = ItemKind.Text, Title = "Shopping list",
                Meta = "Text · 8 min ago", Body = "milk\neggs\nbread\ncoffee",
                TagHex = "#3584e4", Pinned = true, DefaultAction = "copy" });

            items.Add(new MockItem {
                Kind = ItemKind.Code, Title = "State.cs",
                Meta = "Code · C# · 26 min ago",
                Body = "public enum State {\n    Open,\n    Claimed,\n    Resolved\n}",
                TagHex = "#2190a4", Pinned = false, DefaultAction = "copy" });

            items.Add(new MockItem {
                Kind = ItemKind.Image, Title = "gradient.png",
                Meta = "Image · PNG · 1 h ago", Body = "(gradient preview)",
                TagHex = "#3a944a", Pinned = false, DefaultAction = "open-with-default" });

            items.Add(new MockItem {
                Kind = ItemKind.File, Title = "audio.mp3",
                Meta = "File · 150 kB · 2 h ago", Body = "C:\\Media\\audio.mp3",
                TagHex = "#c88800", Pinned = false, DefaultAction = "paste-as-path" });

            items.Add(new MockItem {
                Kind = ItemKind.Files, Title = "3 files",
                Meta = "Files · copy · yesterday",
                Body = "C:\\Docs\\a.txt\nC:\\Docs\\b.txt\nC:\\Docs\\c.png",
                TagHex = "#6f8396", Pinned = false, DefaultAction = "paste-as-path" });

            items.Add(new MockItem {
                Kind = ItemKind.Link, Title = "Never Gonna Give You Up",
                Meta = "Link · youtube.com",
                Body = "Rick Astley — official video",
                TagHex = "#e62d42", Pinned = false, DefaultAction = "open-with-browser" });

            items.Add(new MockItem {
                Kind = ItemKind.Character, Title = "Party popper",
                Meta = "Character · U+1F389", Body = "🎉",
                TagHex = "#d56199", Pinned = false, DefaultAction = "copy" });

            items.Add(new MockItem {
                Kind = ItemKind.Color, Title = "#9c3cbe",
                Meta = "Color · sRGB", Body = "#9c3cbe",
                TagHex = "#9c3cbe", Pinned = false, DefaultAction = "copy-as-hex" });

            return items;
        }

        public static string IconFor(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Text: return "📄";
                case ItemKind.Code: return "⌨";
                case ItemKind.Image: return "🖼";
                case ItemKind.File: return "📁";
                case ItemKind.Files: return "📚";
                case ItemKind.Link: return "🔗";
                case ItemKind.Character: return "😀";
                case ItemKind.Color: return "🎨";
                default: return "•";
            }
        }
    }
}
