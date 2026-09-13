# 05: Card presentation and converters for rich website previews

**What to build:**
Update the link card presentation in `PopupWindow.xaml` and `CompactPopupWindow.xaml` to display the rich website preview (thumbnail image, title, description, and favicon/domain) matching the Copyous reference image (Rick Astley YouTube video).

**Blocked by:** 01

**Status:** resolved

- [x] Add `LinkPreviewImageConverter`, `LinkDescriptionConverter`, and visibility helpers in `PopupConverters.cs`
- [x] Redesign Case 5 in `PopupWindow.xaml`: show thumbnail image on top, title, description, and domain footer when image is present; graceful fallback when not present
- [x] Redesign Case 4 in `CompactPopupWindow.xaml`: show 52x38 thumbnail on left, title, description, and domain on right
- [x] Ensure high-DPI scaling and 60fps scrolling performance
