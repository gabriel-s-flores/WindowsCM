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

    public static string EncodeLink(string? title, string? description, string? image) =>
        JsonSerializer.Serialize(new { title, description, image });

    public static string EncodeCode(string id, string name) =>
        JsonSerializer.Serialize(new { language = new { id, name } });

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
        return JsonSerializer.Serialize(merged);
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

    public static (string? Title, string? Description, string? Image) GetLink(string? json) =>
        (GetString(json, "title"), GetString(json, "description"), GetString(json, "image"));

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
