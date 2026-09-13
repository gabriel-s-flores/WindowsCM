# 01: Website preview service, YouTube resolver, and background metadata fetch

**What to build:**
Enhance `LinkPreviewPorts.cs`, `LinkPreviewService.cs`, and `LinkMetadataParser.cs` to reliably extract OpenGraph / Twitter Cards / YouTube thumbnails (via video ID and oEmbed fallback) and headers for standard websites. Wire background fetching when links are copied or loaded, updating SQLite metadata and disk cache (`LinkImageCache`).

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Add modern browser `User-Agent` and `Accept` headers to `LinkPreviewHttpClient`
- [x] Add YouTube video ID and high-definition thumbnail extraction in `LinkPreviewService`
- [x] Expose `SetMetadata` / `SetMetadataAndTitle` in `IHistoryStore` and `SqliteHistoryStore`
- [x] Wire background preview worker in `App.xaml.cs` to fetch link previews when copied or missing
- [x] Unit tests in `YouTubeAndLinkPreviewTests.cs` verifying YouTube thumbnail extraction and OpenGraph fallback

