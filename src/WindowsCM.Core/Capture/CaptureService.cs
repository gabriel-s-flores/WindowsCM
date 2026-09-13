// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;
using WindowsCM.Core.Tray;

namespace WindowsCM.Core.Capture;

// The capture loop: raw payloads in, typed items in history. Wired by
// ClipboardMonitor (listener events + activation sequence checks); tested
// against real SQLite :memory: plus a temp images dir, with only the OS
// edges faked (reader/event/sequence/process/clock).
//
// Gate order per spec: exclusions, sensitivity hints, incognito. Sensitivity
// is enforced inside Classifier.Probe (null = reject). Only stored copies
// update suppression state: a gated copy (excluded app, sensitive format,
// incognito) records nothing, so the next identical legitimate copy still
// lands. This deliberately deviates from Copyous prev-before-gate parity
// (clipboard.ts:269-274), where an excluded copy poisons the immediate
// re-copy — on Windows that drops user data, so gates win over parity.
// Own-copy suppression still works: copy-back writes go through
// CopiedFromHistory, which always records.
public sealed class CaptureService
{
    private readonly IHistoryStore _store;
    private readonly IImageAssetStore _images;
    private readonly CaptureOptions _options;
    private readonly IClock _clock;

    // Last observed (kind, hash): own-copy suppression and the incognito
    // no-leak record. In-memory only, like incognito itself.
    private (ItemKind Kind, string Hash)? _lastSeen;

    public CaptureService(
        IHistoryStore store,
        IImageAssetStore images,
        CaptureOptions options,
        IClock clock)
    {
        _store = store;
        _images = images;
        _options = options;
        _clock = clock;
    }

    private bool _isIncognito;
    private SqliteHistoryStore? _standaloneIncognitoStore;
    private EphemeralImageAssetStore? _standaloneIncognitoImages;

    // Incognito routes capture to an isolated ephemeral session.
    public bool IsIncognito
    {
        get => _store is IIncognitoToggle t ? t.IsIncognito : _isIncognito;
        set
        {
            if (_store is IIncognitoToggle t)
            {
                t.SetIncognito(value);
            }
            else
            {
                if (value && !_isIncognito)
                {
                    _standaloneIncognitoStore = new SqliteHistoryStore("Data Source=:memory:");
                    _standaloneIncognitoImages = new EphemeralImageAssetStore();
                }
                else if (!value && _isIncognito)
                {
                    _standaloneIncognitoStore?.Dispose();
                    _standaloneIncognitoStore = null;
                    _standaloneIncognitoImages?.Dispose();
                    _standaloneIncognitoImages = null;
                }
            }
            _isIncognito = value;
            _lastSeen = null;
        }
    }

    private IHistoryStore EffectiveStore =>
        (_store is IIncognitoToggle) ? _store : (_standaloneIncognitoStore ?? _store);

    private IImageAssetStore EffectiveImages =>
        (_store is IIncognitoToggle) ? _images : (_standaloneIncognitoImages ?? _images);

    public ClipboardItem? Capture(ClipboardPayload payload, string? processName, DateTime utcNow)
    {
        // Exclusions first: an excluded copy returns before classification and
        // records nothing.
        if (IsExcluded(processName))
        {
            return null;
        }
        var classified = Classifier.Probe(
            payload.Image, payload.Files, payload.Text,
            _options.MaxCharacters, payload.Formats);
        if (classified is null)
        {
            return null;
        }

        var identity = IdentityOf(classified);
        if (_lastSeen == identity)
        {
            return null;
        }
        // Recorded only for copies that will be stored.
        _lastSeen = identity;

        var item = classified switch
        {
            ClassifiedText text => new ClipboardItem(
                text.Kind, text.Content, false, null, utcNow,
                MetadataFor(text, payload.Html), null),
            ClassifiedFile file => new ClipboardItem(
                file.Kind, file.Content, false, null, utcNow, file.MetadataJson, null),
            ClassifiedImage image => new ClipboardItem(
                ItemKind.Image,
                EffectiveImages.SaveIfAbsent(image.FileName, payload.Image!.Data),
                false, null, utcNow, null, null),
            _ => null,
        };
        if (item is null)
        {
            return null;
        }
        return EffectiveStore.AddOrUpdate(item);
    }

    // Convenience for the monitor path (clock + explicit process).
    public ClipboardItem? CaptureNow(ClipboardPayload payload, string? processName) =>
        Capture(payload, processName, _clock.UtcNow);

    // Copy-from-history hook (issue 12 writes the clipboard; this owns the
    // date policy + echo suppression): refreshes datetime only when the
    // setting says so, and always records prev so our own write never echoes.
    public void CopiedFromHistory(long id, DateTime utcNow)
    {
        var item = _store.List().FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return;
        }
        _lastSeen = (item.Kind, SuppressionHash(item));
        if (_options.UpdateDateOnCopy)
        {
            _store.RefreshDate(id, utcNow);
        }
    }

    // Startup orphan sweep: files without a referencing Image item are crash
    // leftovers (commit succeeded, delete never ran). Entries without files
    // are shown with a fallback, never deleted here.
    public void SweepOrphanImages()
    {
        var referenced = _store.List()
            .Where(i => i.Kind == ItemKind.Image)
            .Select(i => FileUris.TryGetFileName(i.Content))
            .OfType<string>();
        _images.SweepOrphans(referenced);
    }

    // CF_HTML stored opaque in v1 (rewritten verbatim by copy-back, issue 12).
    // Text-like metadata is null in v1, so the html envelope owns the column;
    // issue 14 (code language ids) must merge rather than overwrite.
    // Serialize (not string-concat like the fixed-schema operation JSON)
    // because html is untrusted user content requiring escaping.
    private static string? MetadataFor(ClassifiedText text, string? html)
    {
        if (html is null)
        {
            return text.MetadataJson;
        }
        return System.Text.Json.JsonSerializer.Serialize(new { html });
    }

    private static (ItemKind Kind, string Hash) IdentityOf(ClassifiedContent classified) =>        classified switch
        {
            ClassifiedText text => (text.Kind, text.ContentHash),
            ClassifiedFile file => (file.Kind, file.ContentHash),
            ClassifiedImage image => (image.Kind, image.ContentHash),
            _ => throw new ArgumentOutOfRangeException(nameof(classified)),
        };

    // Suppression hash for a stored item, mirroring the classifier hashes:
    // text kinds hash the content, file content is already canonical local
    // paths (hashed verbatim — never re-normalized, so literal % names are
    // safe), images use the bytes hash in the file name (<md5>.<ext>).
    private static string SuppressionHash(ClipboardItem item)
    {
        if (item.Kind == ItemKind.Image)
        {
            // File names are <md5>.<ext> (Classifier parity): the stem is the
            // bytes hash used as the suppression identity.
            var name = FileUris.TryGetFileName(item.Content);
            var stem = name is null ? null : Path.GetFileNameWithoutExtension(name);
            if (!string.IsNullOrEmpty(stem))
            {
                return stem;
            }
        }
        if (item.Kind == ItemKind.File || item.Kind == ItemKind.Files)
        {
            return ClipboardHash.Md5Hex(item.Content);
        }
        return ClipboardHash.Md5Hex(item.Content);
    }

    private bool IsExcluded(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }
        var normalized = StripExe(processName.Trim());
        return _options.ExcludedProcesses.Any(e =>
            StripExe(e.Trim()).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string StripExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
}
