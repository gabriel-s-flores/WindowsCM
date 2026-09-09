# 09: Solution skeleton + history store

**What to build:** a .NET 8 solution that builds clean with a working history
store: items persist in local SQLite, deduplicate on re-copy, clear and
eviction honor pins/tags, and search filters work — all proven by a green
xUnit suite running against in-memory databases.

**Blocked by:** None (can start immediately).

**Status:** implemented

- [x] Solution builds with zero errors; xUnit project runs in CI-local (`dotnet test`)
- [x] Items round-trip (all 8 types, pinned/tagged/dated/metadata/title)
- [x] Re-copy upserts (date bumped, no duplicate, id preserved)
- [x] Property edit colliding on identity returns the conflicting id, unapplied
- [x] Clear keeps pinned-or-tagged by default and wipes all on demand
- [x] Eviction combines over-count and older-than-age, both protection-aware, date-desc ordering
- [x] Search matches body with case-insensitive escape plus title fallback, with pin/tag filters
- [x] Corrupt metadata degrades to null, never drops the item; v1→v2 migration works
- [x] DB path override order (env var, then setting, then LocalAppData default) verified

## Comments

Implemented 2026-09-09 via TDD (red-green per slice) + two-axis code-review.
32/32 xUnit green, 0 errors, 0 warnings (.NET 8.0.425, Microsoft.Data.Sqlite
8.0.10). Review fixes folded in: WAL pragma, Clear/Evict protection flags,
SetPinned/SetTag/SetTitle, Search kind+excludes, RefreshDate rename,
stored-row upsert return, sub-second datetimes, NotSupportedException guard,
path validation, read-loop dedup. Declined: Protection record type (flag
pair stays until a third call site appears). Datetime format locked to
`yyyy-MM-dd HH:mm:ss.fffffff` UTC (legacy second-precision rows still parse).
