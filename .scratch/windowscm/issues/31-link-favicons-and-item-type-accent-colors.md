# 31: Website Favicons for Copied Links and Subtle Color Separation by Item Type

Type: task

Status: resolved

Blocked by: 30

## User Report / Requirements:
1. **Copied Websites and Links with a Favicon/Icon in the Thumbnail**:
   - When a URL/website is copied, that site's official icon (favicon) must be shown in the card's thumbnail / preview in the popup.
   - The card must present a dedicated rich view for links (`LinkPreviewVisibility`): a prominent site icon, a clean domain (e.g. `github.com`), a friendly title/path and a formatted URL.
   - Fast favicon resolution (local disk and memory cache, non-blocking asynchronous download via a high-availability CDN with an elegant fallback to a Fluent web icon when offline).
2. **Subtle Color Separation for Each Item Type**:
   - Visually separate each item type in the clipboard, with Fluent (Windows 11) subtlety and elegance:
     - `Link`: Fluent Blue (`#0078D4` / `#4CC2FF`)
     - `Code`: Violet / Fluent Purple (`#8764B8` / `#B180F0`)
     - `File` / `Files`: Amber / Fluent Soft Orange (`#D97706` / `#F59E0B`)
     - `Image`: Emerald / Fluent Green (`#107C41` / `#36B66B`)
     - `Character` (Emoji/Character): Pink / Soft Coral (`#E83B86` / `#F472B6`)
     - `Color`: The represented color itself, or Magenta
     - `Text`: Neutral Slate Gray / Slate (`#64748B` / `#94A3B8`)
   - Subtle application:
     - Type icon in the card header colored with the type's accent color.
     - Vertical indicator in the header: if the item has no manual Tag set by the user, it shows the item type's color as a soft indicator.
     - Type badge / pill in the card footer (`KindLabel`) with a subtle translucent background (tinted background ~10-14%) and an indicator dot in the type's color.
     - Native support for both the Windows 11 Light and Dark themes.

## Answer
Implemented and validated on 2026-09-12 following Matt Pocock's skills (`codebase-design`, `domain-modeling` and `tdd`):

1. **Official Website Favicons in Link Thumbnails (`ItemKind.Link`)**:
   - Created `LinkDisplayHelper.cs` in `WindowsCM.Core.Popup`: a pure module for clean host/domain extraction (`GetDomain`, stripping `www.`, ports and parameters), canonical resolution of the high-definition favicon endpoint (`GetFaviconCdnUrl`), formatting of friendly paths and titles (`GetPathOrTitle`) and sanitized display of URLs (`GetDisplayUrl`).
   - Created `FaviconService.cs` in `WindowsCM.App`: a manager with a 2-level cache (RAM in a `ConcurrentDictionary` with frozen `BitmapSource`s for 0ms rendering; and disk, persisted in `%LocalAppData%\WindowsCM\favicons\{domain}.png`). It fetches favicons asynchronously in the background via a resilient global CDN (Google Favicons API sz=64), without blocking the capture loop or the UI. It reactively notifies the UI (`FaviconUpdated`) so the link cards update as soon as the icon is ready.
   - Created a dedicated `LinkPreviewVisibility` case in the card template in `PopupWindow.xaml`: it replaces the raw text display with an elegant 46x46 px Fluent container with rounded corners holding the site's official favicon (or the Fluent `\uE774` icon as a graceful fallback when offline), the domain in semi-bold, the page title or path, and the formatted URL.

2. **Subtle Color Separation by Item Type (Fluent Windows 11)**:
   - Created `ItemTypeTheme.cs` in `WindowsCM.Core.Popup`: a deterministic semantic mapping of accent colors and translucent backgrounds (12-16% alpha) for the domain's 8 types (`Link`, `Code`, `File`/`Files`, `Image`, `Character`, `Color`, `Text`) with balanced contrast in both the Light and Dark themes. Color items dynamically take on the copied color itself.
   - Updated `PopupThemeBrushes.cs` with the semantic brushes `KindLinkBrush`, `KindCodeBrush`, `KindFileBrush`, etc. and a link/favicon container palette.
   - Created the `KindBrushConverter`, `KindBackgroundBrushConverter` and `CardIndicatorBrushConverter` converters in `PopupConverters.cs`.
   - In `PopupWindow.xaml`:
     - The type icon in the card header now dynamically receives its type's accent color (`KindBrush`).
     - The 3x14 px side indicator bar shows the manual tag if the user has classified the item; if there is no manual tag, it subtly shows the item type's color.
     - The bottom type subtitle was turned into a Fluent pill / badge with rounded corners (`CornerRadius="4"`), a soft translucent background (`KindBackgroundBrush`), an indicator dot and semi-bold typography in the type's color.

3. **Unit Tests and Quality (TDD)**:
   - Created `LinkDisplayHelperTests.cs` and `ItemTypeThemeTests.cs`.
   - A total of 827 tests run with a 100% pass rate and zero failures (`dotnet test`).
   - `WindowsCM.App` builds without any error or warning.
