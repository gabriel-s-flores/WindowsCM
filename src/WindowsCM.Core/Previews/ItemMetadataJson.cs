// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;

namespace WindowsCM.Core.Previews;

// Typed access to the metadata JSON column (Copyous Metadata parity:
// CodeMetadata{language{id,name}} | FileMetadata{operation} |
// LinkMetadata{title,description,image}, plus the v1 CF_HTML {html}
// envelope). Setters merge — CaptureService stores {html} first, issue 14
// adds language/link keys without overwriting it. Getters degrade corrupt
// JSON to null, never dropping the item (store parity).
public static class ItemMetadataJson
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // The default encoder writes every <, >, &, ' and non-ASCII character as
    // a six-character escape: the CF_HTML of a browser or VS Code copy took
    // up to six times its size in the database and on every read. This JSON
    // is only ever parsed back, never embedded in a page.
    public static readonly JsonSerializerOptions StorageOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string EncodeLink(string? title, string? description, string? image) =>
        JsonSerializer.Serialize(new { title, description, image }, StorageOptions);

    public static string EncodeCode(string id, string name) =>
        JsonSerializer.Serialize(new { language = new { id, name } }, StorageOptions);

    // Merges overlay keys into the base object; corrupt base counts as {}.
    // Top-level keys merge (so {language:{...}} joins {html:...} without
    // clobbering it); overlay wins per key, case-insensitively.
    public static string Merge(string? baseJson, string overlayJson)
    {
        var merged = ReadObject(baseJson);
        foreach (var (key, value) in ReadObject(overlayJson))
        {
            merged[key] = value;
        }
        return JsonSerializer.Serialize(merged, StorageOptions);
    }

    // A re-copy's metadata over the item's own: the new copy's keys win,
    // enrichment it does not carry (link preview, code language) stays, and
    // old CF_HTML goes when the new copy has none — pasting from history
    // would otherwise put back formatting the latest copy did not have.
    public static string? OnRecopy(string? existing, string? incoming)
    {
        var kept = ReadObject(existing);
        kept.Remove("html");
        foreach (var (key, value) in ReadObject(incoming))
        {
            kept[key] = value;
        }
        return kept.Count == 0 ? null : JsonSerializer.Serialize(kept, StorageOptions);
    }

    public static string? GetString(string? json, string property)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            foreach (var prop in document.RootElement.EnumerateObject())
            {
                if (prop.Name.Equals(property, StringComparison.OrdinalIgnoreCase))
                {
                    return prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString()
                        : null;
                }
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static (string? Id, string? Name) GetLanguage(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return (null, null);
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }
            foreach (var prop in document.RootElement.EnumerateObject())
            {
                if (prop.Name.Equals("language", StringComparison.OrdinalIgnoreCase)
                    && prop.Value.ValueKind == JsonValueKind.Object)
                {
                    return (GetChild(prop.Value, "id"), GetChild(prop.Value, "name"));
                }
            }
            return (null, null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    // A link card binds seven converters to one item's link fields; each
    // parsed the whole metadata three times (21 parses per card, and the
    // metadata can hold hundreds of KB of CF_HTML). One parse per metadata
    // string now: the same instance is shared by every binding of the card.
    public static (string? Title, string? Description, string? Image) GetLink(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return (null, null, null);
        }
        var fields = LinkCache.GetValue(json, ParseLink);
        return (fields.Title, fields.Description, fields.Image);
    }

    private sealed record LinkFields(string? Title, string? Description, string? Image);

    private static readonly LinkFields NoLink = new(null, null, null);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string, LinkFields> LinkCache = new();

    // GetString semantics per field: the first property with the name
    // (case-insensitive) wins, and only a string value counts.
    private static LinkFields ParseLink(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return NoLink;
            }
            string? title = null, description = null, image = null;
            bool seenTitle = false, seenDescription = false, seenImage = false;
            foreach (var prop in document.RootElement.EnumerateObject())
            {
                var value = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
                if (!seenTitle && prop.Name.Equals("title", StringComparison.OrdinalIgnoreCase))
                {
                    (title, seenTitle) = (value, true);
                }
                else if (!seenDescription && prop.Name.Equals("description", StringComparison.OrdinalIgnoreCase))
                {
                    (description, seenDescription) = (value, true);
                }
                else if (!seenImage && prop.Name.Equals("image", StringComparison.OrdinalIgnoreCase))
                {
                    (image, seenImage) = (value, true);
                }
            }
            return new LinkFields(title, description, image);
        }
        catch (JsonException)
        {
            return NoLink;
        }
    }

    private static string? GetChild(JsonElement element, string name)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()
                    : null;
            }
        }
        return null;
    }

    private static Dictionary<string, object?> ReadObject(string? json)
    {
        Dictionary<string, object?> parsed;
        if (string.IsNullOrEmpty(json))
        {
            parsed = new Dictionary<string, object?>(StringComparer.Ordinal);
        }
        else
        {
            try
            {
                parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, ReadOptions)
                    ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            }
            catch (JsonException)
            {
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            }
        }
        return new Dictionary<string, object?>(parsed, StringComparer.OrdinalIgnoreCase);
    }
}
