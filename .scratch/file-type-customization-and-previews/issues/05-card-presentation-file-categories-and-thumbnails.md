# 05: Card Presentation for File Categories and Rich Thumbnails

**What to build:** Update popup card converters and templates so that File/Files items display the specific accent color and category label (e.g. "Apresentação PowerPoint" in orange, "Vídeo MP4" in red), and render high-resolution thumbnails for any file where a thumbnail exists, falling back to system icon and details view when unavailable.

**Blocked by:** 01, 03, 04

**Status:** resolved

- [x] `ItemDisplayFormatter` enhanced to format titles, types and category labels based on configured file categories
- [x] Card indicator stripe and type badge derive color from the file's category
- [x] Card body binds to `ThumbnailService` / `FileThumbnailConverter` to render rich previews for video/photo/doc files
- [x] Seamless fallback to system icon and metadata summary when no thumbnail is present
- [x] Unit tests for display formatting and converters
