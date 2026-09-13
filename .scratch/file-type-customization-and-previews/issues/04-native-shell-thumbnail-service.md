# 04: Native Windows Shell Thumbnail Extraction Service

**What to build:** Implement `ThumbnailService` in `WindowsCM.App` using COM interop with `IShellItemImageFactory` and `SHCreateItemFromParsingName` to extract high-resolution native thumbnails (photos, videos, audio cover art, presentation slides, PDFs) with memory caching and thread-safe WPF `BitmapSource`.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] COM declarations for `IShellItemImageFactory` and Win32 interop helpers
- [x] Safe thumbnail extraction with `SIIGBF_THUMBNAILONLY` / `SIIGBF_BIGGERSIZEOK`
- [x] Memory caching with `ConcurrentDictionary` and timestamp invalidation
- [x] Asynchronous extraction pattern to prevent blocking the UI thread
- [x] Fallback handling when thumbnail extraction is not supported for a given file type
