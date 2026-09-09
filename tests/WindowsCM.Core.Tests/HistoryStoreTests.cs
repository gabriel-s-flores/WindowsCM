// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests;

public sealed class HistoryStoreTests : IDisposable
{
    private readonly SqliteHistoryStore _store = new("Data Source=:memory:");

    public void Dispose() => _store.Dispose();

    private static ClipboardItem Sample(
        ItemKind kind = ItemKind.Text,
        string content = "hello",
        bool pinned = false,
        string? tag = null,
        string? metadataJson = null,
        string? title = null,
        DateTime? capturedAt = null) => new(
            Kind: kind,
            Content: content,
            Pinned: pinned,
            Tag: tag,
            CapturedAt: capturedAt ?? new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc),
            MetadataJson: metadataJson,
            Title: title);

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var saved = _store.AddOrUpdate(Sample(
            kind: ItemKind.Code,
            content: "Console.WriteLine();",
            pinned: true,
            tag: "blue",
            metadataJson: """{"language":{"id":"csharp","name":"C#"}}""",
            title: "snippet"));

        var listed = Assert.Single(_store.List());

        Assert.Equal(saved.Id, listed.Id);
        Assert.Equal(ItemKind.Code, listed.Kind);
        Assert.Equal("Console.WriteLine();", listed.Content);
        Assert.True(listed.Pinned);
        Assert.Equal("blue", listed.Tag);
        Assert.Equal("""{"language":{"id":"csharp","name":"C#"}}""", listed.MetadataJson);
        Assert.Equal("snippet", listed.Title);
        Assert.Equal(
            new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc),
            listed.CapturedAt);
    }

    [Theory]
    [InlineData(ItemKind.Text)]
    [InlineData(ItemKind.Code)]
    [InlineData(ItemKind.Image)]
    [InlineData(ItemKind.File)]
    [InlineData(ItemKind.Files)]
    [InlineData(ItemKind.Link)]
    [InlineData(ItemKind.Character)]
    [InlineData(ItemKind.Color)]
    public void RoundTrip_AllEightKinds(ItemKind kind)
    {
        _store.AddOrUpdate(Sample(kind, content: "content-" + kind));

        var listed = Assert.Single(_store.List());

        Assert.Equal(kind, listed.Kind);
        Assert.Equal("content-" + kind, listed.Content);
    }

    [Fact]
    public void List_ReturnsNewestFirst()
    {
        _store.AddOrUpdate(Sample(content: "old",
            capturedAt: new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc)));
        _store.AddOrUpdate(Sample(content: "new",
            capturedAt: new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc)));

        var listed = _store.List();

        Assert.Equal(["new", "old"], listed.Select(i => i.Content));
    }

    [Fact]
    public void Recopy_BumpsDateKeepsIdWithoutDuplicating()
    {
        var first = _store.AddOrUpdate(Sample(content: "same",
            capturedAt: new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc)));

        var second = _store.AddOrUpdate(Sample(content: "same",
            capturedAt: new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(first.Id, second.Id);
        var listed = Assert.Single(_store.List());
        Assert.Equal(first.Id, listed.Id);
        Assert.Equal(
            new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc),
            listed.CapturedAt);
    }

    [Fact]
    public void TryUpdateContent_CollidingIdentity_ReturnsConflictingIdUnapplied()
    {
        var a = _store.AddOrUpdate(Sample(content: "a"));
        var b = _store.AddOrUpdate(Sample(content: "b"));

        var result = _store.TryUpdateContent(b.Id, ItemKind.Text, "a");

        Assert.Equal(a.Id, result);
        Assert.Equal("b", _store.List().Single(i => i.Id == b.Id).Content);
    }

    [Fact]
    public void TryUpdateContent_FreeEdit_AppliesAndReturnsMinusOne()
    {
        var saved = _store.AddOrUpdate(Sample(content: "before"));

        var result = _store.TryUpdateContent(saved.Id, ItemKind.Code, "after");

        Assert.Equal(-1, result);
        var edited = _store.List().Single(i => i.Id == saved.Id);
        Assert.Equal(ItemKind.Code, edited.Kind);
        Assert.Equal("after", edited.Content);
    }

    [Fact]
    public void Clear_KeepProtected_RemovesOnlyUnprotected()
    {
        _store.AddOrUpdate(Sample(content: "pinned", pinned: true));
        _store.AddOrUpdate(Sample(content: "tagged", tag: "red"));
        _store.AddOrUpdate(Sample(content: "plain"));

        var removed = _store.Clear(keepProtected: true);

        Assert.Equal(1, removed);
        Assert.Equal(["pinned", "tagged"], _store.List().Select(i => i.Content).Order());
    }

    [Fact]
    public void Clear_WipeAll_RemovesEverything()
    {
        _store.AddOrUpdate(Sample(content: "pinned", pinned: true));
        _store.AddOrUpdate(Sample(content: "plain"));

        var removed = _store.Clear(keepProtected: false);

        Assert.Equal(2, removed);
        Assert.Empty(_store.List());
    }

    [Fact]
    public void Evict_OverCount_RemovesOldestUnprotected()
    {
        var t = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        _store.AddOrUpdate(Sample(content: "old-pinned", pinned: true,
            capturedAt: t.AddHours(-5)));
        _store.AddOrUpdate(Sample(content: "t1", capturedAt: t.AddHours(-4)));
        _store.AddOrUpdate(Sample(content: "t2", capturedAt: t.AddHours(-3)));
        _store.AddOrUpdate(Sample(content: "t3", capturedAt: t.AddHours(-2)));
        _store.AddOrUpdate(Sample(content: "t4", capturedAt: t.AddHours(-1)));

        var removed = _store.Evict(maxCount: 3, maxAgeMinutes: 0, utcNow: t);

        Assert.Equal(1, removed);
        Assert.Equal(["old-pinned", "t2", "t3", "t4"],
            _store.List().Select(i => i.Content).Order());
    }

    [Fact]
    public void Evict_OlderThanAge_RemovesStaleButKeepsProtected()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        _store.AddOrUpdate(Sample(content: "stale", capturedAt: now.AddHours(-2)));
        _store.AddOrUpdate(Sample(content: "fresh", capturedAt: now.AddMinutes(-10)));
        _store.AddOrUpdate(Sample(content: "stale-pinned", pinned: true,
            capturedAt: now.AddHours(-3)));

        var removed = _store.Evict(maxCount: 50, maxAgeMinutes: 60, utcNow: now);

        Assert.Equal(1, removed);
        Assert.Equal(["fresh", "stale-pinned"],
            _store.List().Select(i => i.Content).Order());
    }

    [Fact]
    public void Evict_UnprotectedTag_WhenProtectionOff()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        _store.AddOrUpdate(Sample(content: "tagged", tag: "red",
            capturedAt: now.AddHours(-2)));

        var removed = _store.Evict(maxCount: 50, maxAgeMinutes: 60, utcNow: now,
            protectPinned: true, protectTagged: false);

        Assert.Equal(1, removed);
        Assert.Empty(_store.List());
    }

    [Fact]
    public void Search_MatchesBodyCaseInsensitively()
    {
        _store.AddOrUpdate(Sample(content: "Hello World"));
        _store.AddOrUpdate(Sample(content: "other"));

        var found = _store.Search("hello");

        Assert.Equal(["Hello World"], found.Select(i => i.Content));
    }

    [Fact]
    public void Search_TreatsWildcardsLiterally()
    {
        _store.AddOrUpdate(Sample(content: "100%"));
        _store.AddOrUpdate(Sample(content: "1000"));

        var found = _store.Search("100%");

        Assert.Equal(["100%"], found.Select(i => i.Content));
    }

    [Fact]
    public void Search_FallsBackToTitle()
    {
        _store.AddOrUpdate(Sample(content: "milk", title: "shopping"));

        var found = _store.Search("shop");

        Assert.Equal(["milk"], found.Select(i => i.Content));
    }

    [Fact]
    public void Search_PinAndTagFilters()
    {
        _store.AddOrUpdate(Sample(content: "a", pinned: true));
        _store.AddOrUpdate(Sample(content: "b", tag: "red"));
        _store.AddOrUpdate(Sample(content: "c"));

        Assert.Equal(["a"], _store.Search("", pinned: true).Select(i => i.Content));
        Assert.Equal(["b"], _store.Search("", tag: "red").Select(i => i.Content));
        Assert.Equal(3, _store.Search("").Count);
    }

    [Fact]
    public void RefreshDate_RefreshesDateToTop()
    {
        var t = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        var first = _store.AddOrUpdate(Sample(content: "first",
            capturedAt: t.AddHours(-2)));
        _store.AddOrUpdate(Sample(content: "second", capturedAt: t.AddHours(-1)));

        _store.RefreshDate(first.Id, t);

        var listed = _store.List();
        Assert.Equal("first", listed[0].Content);
        Assert.Equal(t, listed[0].CapturedAt);
    }

    [Fact]
    public void CorruptMetadata_ReadsAsNullKeepingItem()
    {
        _store.AddOrUpdate(Sample(content: "x", metadataJson: "{not json"));

        var listed = Assert.Single(_store.List());

        Assert.Equal("x", listed.Content);
        Assert.Null(listed.MetadataJson);
    }

    [Fact]
    public void Migration_V1WithoutTitle_UpgradesKeepingRows()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            using (var setup = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=false"))
            {
                setup.Open();
                using var ddl = setup.CreateCommand();
                ddl.CommandText = """
                    CREATE TABLE clipboard (
                      id INTEGER PRIMARY KEY AUTOINCREMENT, type TEXT NOT NULL,
                      content TEXT NOT NULL, pinned INTEGER NOT NULL DEFAULT 0,
                      tag TEXT NULL, datetime TEXT NOT NULL, metadata TEXT NULL,
                      UNIQUE (type, content));
                    CREATE TABLE clipboard_version (id INTEGER PRIMARY KEY CHECK (id = 1), version INTEGER NOT NULL);
                    INSERT INTO clipboard_version (id, version) VALUES (1, 1);
                    INSERT INTO clipboard (type, content, pinned, tag, datetime, metadata)
                    VALUES ('Text', 'legacy', 0, NULL, '2026-09-09 11:00:00', NULL);
                    """;
                ddl.ExecuteNonQuery();
            }

            using var migrated = new SqliteHistoryStore($"Data Source={path};Pooling=false");
            var listed = Assert.Single(migrated.List());

            Assert.Equal("legacy", listed.Content);
            Assert.Null(listed.Title);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DatabasePaths_EnvOverridesSettingOverridesDefault()
    {
        const string variable = "WINDOWSCM_DBPATH";
        var previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, "E:\\custom\\c.db");
            Assert.Equal("E:\\custom\\c.db", DatabasePaths.Resolve("C:\\s\\s.db"));

            Environment.SetEnvironmentVariable(variable, "");
            Assert.Equal("C:\\s\\s.db", DatabasePaths.Resolve("C:\\s\\s.db"));

            Environment.SetEnvironmentVariable(variable, null);
            var @default = DatabasePaths.Resolve(null);
            Assert.EndsWith(Path.Combine("WindowsCM", "clipboard.db"), @default);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public void Recopy_ReturnsStoredFlagsNotCallerOnes()
    {
        _store.AddOrUpdate(Sample(content: "same", pinned: true, tag: "blue",
            capturedAt: new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc)));

        var bumped = _store.AddOrUpdate(Sample(content: "same",
            capturedAt: new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc)));

        Assert.True(bumped.Pinned);
        Assert.Equal("blue", bumped.Tag);
    }

    [Fact]
    public void RoundTrip_KeepsSubSecondPrecision()
    {
        var stamp = new DateTime(2026, 9, 9, 12, 0, 0, 500, DateTimeKind.Utc);
        _store.AddOrUpdate(Sample(content: "x", capturedAt: stamp));

        Assert.Equal(stamp, Assert.Single(_store.List()).CapturedAt);
    }

    [Fact]
    public void Clear_RespectsProtectionFlags()
    {
        _store.AddOrUpdate(Sample(content: "pinned", pinned: true));
        _store.AddOrUpdate(Sample(content: "tagged", tag: "red"));

        _store.Clear(keepProtected: true, protectPinned: true, protectTagged: false);

        Assert.Equal(["pinned"], _store.List().Select(i => i.Content));
    }

    [Fact]
    public void SetPin_Tag_Title_Persist()
    {
        var saved = _store.AddOrUpdate(Sample(content: "x"));

        _store.SetPinned(saved.Id, true);
        _store.SetTag(saved.Id, "green");
        _store.SetTitle(saved.Id, "renamed");

        var edited = Assert.Single(_store.List());
        Assert.True(edited.Pinned);
        Assert.Equal("green", edited.Tag);
        Assert.Equal("renamed", edited.Title);
    }

    [Fact]
    public void Search_KindAndExcludeFilters()
    {
        _store.AddOrUpdate(Sample(kind: ItemKind.Code, content: "print(1)"));
        _store.AddOrUpdate(Sample(kind: ItemKind.Text, content: "print(2)", pinned: true));
        _store.AddOrUpdate(Sample(kind: ItemKind.Text, content: "print(3)", tag: "red"));

        Assert.Equal(["print(1)"],
            _store.Search("print", kind: ItemKind.Code).Select(i => i.Content));
        Assert.Equal(2, _store.Search("print", excludePinned: true).Count);
        Assert.Equal(2, _store.Search("print", excludeTagged: true).Count);
    }

    [Fact]
    public void DatabasePaths_Validate_RejectsBadExtensionAndMissingDir()
    {
        Assert.NotNull(DatabasePaths.Validate("C:\\x\\history.txt"));
        Assert.NotNull(DatabasePaths.Validate("C:\\no-such-dir-xyz\\c.db"));
        Assert.Null(DatabasePaths.Validate(Path.GetTempPath() + "c.db"));
    }
}
