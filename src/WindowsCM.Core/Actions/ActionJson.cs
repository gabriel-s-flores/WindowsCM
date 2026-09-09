// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Actions;

// actions.json serialization (Copyous `loadConfig`/`saveConfig` parity).
// Reader tolerance (research 05 §1.2): case-insensitive names, comments and
// trailing commas allowed, unknown properties ignored (forward-compat),
// unknown type names dropped, unknown output falls back to ignore, unknown
// action kinds survive as UnknownAction. A structurally corrupt file throws
// and the store falls back to built-ins — never a half-loaded config.
public static class ActionsJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters =
        {
            new ActionConfigConverter(),
            new ActionNodeConverter(),
        },
    };

    public static string Serialize(ActionConfig config) =>
        JsonSerializer.Serialize(config, Options);

    public static ActionConfig Deserialize(string json) =>
        JsonSerializer.Deserialize<ActionConfig>(json, Options)
            ?? throw new JsonException("actions.json deserialized to null.");

    // JsonElement lookup is ordinal case-sensitive; the options-level
    // case-insensitivity only covers model binding, so manual DOM reads
    // fold case themselves (hand-edited files use any casing).
    internal static bool TryProperty(JsonElement el, string name, out JsonElement value)
    {
        foreach (var prop in el.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
}

// CSS Color 4 kebab-case spellings used in actions.json ("linear-rgb").
// The Classification enum is PascalCase; this mapping is the only bridge.
public static class ActionColorSpaces
{
    private static readonly Dictionary<string, ColorSpace> ByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["rgb"] = ColorSpace.Rgb,
            ["hex"] = ColorSpace.Hex,
            ["hsl"] = ColorSpace.Hsl,
            ["hwb"] = ColorSpace.Hwb,
            ["linear-rgb"] = ColorSpace.LinearRgb,
            ["xyz"] = ColorSpace.Xyz,
            ["lab"] = ColorSpace.Lab,
            ["lch"] = ColorSpace.Lch,
            ["oklab"] = ColorSpace.Oklab,
            ["oklch"] = ColorSpace.Oklch,
        };

    private static readonly Dictionary<ColorSpace, string> BySpace =
        ByName.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static bool TryParse(string? name, out ColorSpace space)
    {
        space = default;
        return name is not null && ByName.TryGetValue(name, out space);
    }

    public static string Name(ColorSpace space) => BySpace[space];
}

internal sealed class ActionConfigConverter : JsonConverter<ActionConfig>
{
    public override ActionConfig? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Action config must be a JSON object.");
        }
        if (!ActionsJson.TryProperty(root, "actions", out var actionsEl)
            || actionsEl.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Action config misses the actions array.");
        }
        var actions = actionsEl.EnumerateArray()
            .Select(el => JsonSerializer.Deserialize<ActionNode>(el.GetRawText(), options))
            .OfType<ActionNode>()
            .ToList();
        var defaults = new Dictionary<ItemKind, string>();
        if (ActionsJson.TryProperty(root, "defaults", out var defaultsEl)
            && defaultsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in defaultsEl.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String
                    && Enum.TryParse<ItemKind>(prop.Name, ignoreCase: true, out var kind)
                    && prop.Value.GetString() is { } id)
                {
                    defaults[kind] = id;
                }
            }
        }
        return new ActionConfig(actions, defaults);
    }

    public override void Write(
        Utf8JsonWriter writer, ActionConfig value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("actions");
        foreach (var node in value.Actions)
        {
            JsonSerializer.Serialize(writer, node, options);
        }
        writer.WriteEndArray();
        writer.WriteStartObject("defaults");
        foreach (var (kind, id) in value.Defaults)
        {
            writer.WriteString(kind.ToString(), id);
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}

internal sealed class ActionNodeConverter : JsonConverter<ActionNode>
{
    public override ActionNode? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var el = doc.RootElement;
        if (el.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Action node must be a JSON object.");
        }
        if (ActionsJson.TryProperty(el, "kind", out var kindEl)
            && kindEl.ValueKind == JsonValueKind.String)
        {
            return ReadAction(el, kindEl.GetString()!, options);
        }
        if (ActionsJson.TryProperty(el, "actions", out var children)
            && children.ValueKind == JsonValueKind.Array)
        {
            var name = RequiredString(el, "name");
            var nested = children.EnumerateArray()
                .Select(child => JsonSerializer.Deserialize<ActionNode>(child.GetRawText(), options))
                .OfType<ActionNode>()
                .ToList();
            return new ActionSubmenu(name, nested);
        }
        throw new JsonException("Action node has neither kind nor actions.");
    }

    private static ActionNode ReadAction(JsonElement el, string kind, JsonSerializerOptions options)
    {
        var id = RequiredString(el, "id");
        var name = RequiredString(el, "name");
        var pattern = OptionalString(el, "pattern");
        var types = OptionalTypes(el);
        var output = OptionalOutput(el);
        var shortcut = OptionalStrings(el, "shortcut");
        return kind switch
        {
            "command" => new CommandAction(
                id, name, RequiredString(el, "command"), pattern, types, output, shortcut),
            "color" => OptionalString(el, "space") is { } space
                && ActionColorSpaces.TryParse(space, out var parsed)
                    ? new ColorAction(
                        id, name, parsed, pattern, types, output, shortcut)
                    : new UnknownAction(kind, id, name, pattern, types, output, shortcut),
            // Output is fixed to ignore by schema; a stray value is not honored.
            "qrcode" => new QrCodeAction(id, name, pattern, types, shortcut),
            _ => new UnknownAction(kind, id, name, pattern, types, output, shortcut),
        };
    }

    private static string RequiredString(JsonElement el, string property)
    {
        if (ActionsJson.TryProperty(el, property, out var value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { } text)
        {
            return text;
        }
        throw new JsonException($"Action misses required string '{property}'.");
    }

    private static string? OptionalString(JsonElement el, string property) =>
        ActionsJson.TryProperty(el, property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static List<ItemKind>? OptionalTypes(JsonElement el)
    {
        if (!ActionsJson.TryProperty(el, "types", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Action types must be an array.");
        }
        // Unknown kind names are dropped (closed eight-kind set; never
        // triggers on files our writer produced).
        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .Where(name => Enum.TryParse<ItemKind>(name, ignoreCase: true, out _))
            .Select(name => Enum.Parse<ItemKind>(name, ignoreCase: true))
            .ToList();
    }

    private static ActionOutput OptionalOutput(JsonElement el) =>
        ActionsJson.TryProperty(el, "output", out var value)
            && value.ValueKind == JsonValueKind.String
            && Enum.TryParse<ActionOutput>(value.GetString(), ignoreCase: true, out var output)
            ? output
            : ActionOutput.Ignore;

    private static List<string>? OptionalStrings(JsonElement el, string property)
    {
        if (!ActionsJson.TryProperty(el, property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Action '{property}' must be an array.");
        }
        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToList();
    }

    public override void Write(
        Utf8JsonWriter writer, ActionNode value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case CommandAction command:
                writer.WriteStartObject();
                writer.WriteString("kind", "command");
                writer.WriteString("id", command.Id);
                writer.WriteString("name", command.Name);
                writer.WriteString("command", command.Command);
                WriteCommon(writer, command);
                writer.WriteEndObject();
                break;
            case ColorAction color:
                writer.WriteStartObject();
                writer.WriteString("kind", "color");
                writer.WriteString("id", color.Id);
                writer.WriteString("name", color.Name);
                writer.WriteString("space", ActionColorSpaces.Name(color.Space));
                WriteCommon(writer, color);
                writer.WriteEndObject();
                break;
            case QrCodeAction qr:
                writer.WriteStartObject();
                writer.WriteString("kind", "qrcode");
                writer.WriteString("id", qr.Id);
                writer.WriteString("name", qr.Name);
                WriteCommon(writer, qr);
                writer.WriteEndObject();
                break;
            case UnknownAction unknown:
                writer.WriteStartObject();
                writer.WriteString("kind", unknown.Kind);
                writer.WriteString("id", unknown.Id);
                writer.WriteString("name", unknown.Name);
                WriteCommon(writer, unknown);
                writer.WriteEndObject();
                break;
            case ActionSubmenu submenu:
                writer.WriteStartObject();
                writer.WriteString("name", submenu.Name);
                writer.WriteStartArray("actions");
                foreach (var child in submenu.Actions)
                {
                    JsonSerializer.Serialize(writer, child, options);
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
                break;
            default:
                throw new JsonException($"Cannot serialize action node '{value.GetType().Name}'.");
        }
    }

    private static void WriteCommon(Utf8JsonWriter writer, ClipboardAction action)
    {
        if (action.Pattern is null)
        {
            writer.WriteNull("pattern");
        }
        else
        {
            writer.WriteString("pattern", action.Pattern);
        }
        if (action.Types is null)
        {
            writer.WriteNull("types");
        }
        else
        {
            writer.WriteStartArray("types");
            foreach (var kind in action.Types)
            {
                writer.WriteStringValue(kind.ToString());
            }
            writer.WriteEndArray();
        }
        writer.WriteString("output", action.Output.ToString().ToLowerInvariant());
        if (action.Shortcut is null)
        {
            writer.WriteNull("shortcut");
        }
        else
        {
            writer.WriteStartArray("shortcut");
            foreach (var key in action.Shortcut)
            {
                writer.WriteStringValue(key);
            }
            writer.WriteEndArray();
        }
    }
}
