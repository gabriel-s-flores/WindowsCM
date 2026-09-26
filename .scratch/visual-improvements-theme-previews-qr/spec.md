# Spec: Rich Website Previews, Theme Switching (Dark/Light/High Contrast) and QR Code Button on Cards

Status: ready-for-agent

## Problem Statement

1. **Website Previews**: Currently, web links copied into the WindowsCM history show only a favicon icon and the domain as text, without loading the preview image (OpenGraph / Twitter Card / YouTube thumbnail) and without showing the page's description or full title, diverging from the original Copyous, where YouTube videos (such as the Rick Astley reference image) and web pages appear as rich preview cards with a thumbnail and well-defined details. In addition, the existing `LinkPreviewService` was never triggered in the background during capture.
2. **Themes (Dark, Light and High Contrast Mode)**: WindowsCM does not offer an option in the interface for the user to manually switch between the dark and light themes, nor does it have a High Contrast mode for visual accessibility and heightened contrast (black `#000000` background, white `#FFFFFF` borders and high-visibility focus/selection highlighting).
3. **QR Code in the Cards' Quick Menu**: To generate a QR code for an item, the user has to open the secondary menu ("...") or use the `Ctrl+Q` shortcut. There is no direct QR Code button in the quick menu on the bottom bar of each card.

## Solution

1. **Website Metadata and Preview Service**:
   - Integrate support for YouTube URLs with video ID extraction (`watch?v=`, `youtu.be/`, `shorts/`), direct resolution of the high-definition thumbnail (`https://img.youtube.com/vi/{id}/hqdefault.jpg`) and a fallback/oEmbed for titles and authors.
   - Update `LinkPreviewHttpClient` with modern browser headers to avoid being blocked by common websites and to support downloading images with appropriate headers.
   - Trigger `LinkPreviewService` in the background when links are copied or shown, saving the image to the disk cache (`LinkImageCache`) and updating `MetadataJson` and `Title` in the SQLite database.
2. **Full High Contrast Support and Theme Switching**:
   - Implement a High Contrast palette in `PopupThemeBrushes` (pure black, white borders with a crisp thickness, high-contrast white text, cyan/yellow highlight for selection and pinned items).
   - Add a theme selection control to the Settings screen (`SettingsWindow`) with options for Dark Mode, Light Mode, High Contrast and Follow Windows, with persistence and immediate dynamic updating in all windows.
3. **QR Code Quick Action Button**:
   - Add a button with the official QR Code icon (`\uED14`) to the quick action bar of each card in `PopupWindow` and `CompactPopupWindow`, directly opening the QR Code dialog for compatible items.
4. **Build and Packaging**:
   - Build the release `win-x64` publish, generate the portable zip file in `dist/` and build the Inno Setup installer with `ISCC.exe`.

## User Stories

1. As a user, when I copy a link to a YouTube video or website, I want to see a rich preview card with a high-definition thumbnail image, title, and description, just like in Copyous.
2. As a user, I want to be able to switch between Dark mode, Light mode, and High Contrast mode in Settings, so that the app matches my visual preference and accessibility needs.
3. As a user with visual impairments, I want a High Contrast mode that provides pure black backgrounds and high-contrast white borders and text.
4. As a user, I want a direct QR code button on the quick action bar of each clipboard card, so that I can generate a QR code with a single click without opening submenus.
5. As a user, I want both the portable zip and installer executable to be updated and ready to use after the improvements are completed.
