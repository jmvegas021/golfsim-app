using System.Text.Json;
using System.Text.Json.Serialization;

namespace GsproLighting.Core.Config;

/// <summary>
/// Loads legacy <c>{ "R", "G", "B" }</c> as an <see cref="EffectSlot"/> with Solid animation.
/// Writes the full EffectSlot shape.
/// </summary>
public sealed class EffectSlotJsonConverter : JsonConverter<EffectSlot>
{
    public override EffectSlot Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected object for EffectSlot.");

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (IsLegacyRgb(root))
        {
            var color = DeserializeColor(root);
            return EffectSlot.Curated(color, EffectAnimations.Solid);
        }

        return ReadEffectSlot(root);
    }

    public override void Write(Utf8JsonWriter writer, EffectSlot value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("Color");
        JsonSerializer.Serialize(writer, value.Color ?? RgbColor.FromRgb(0, 0, 0), options);
        writer.WriteString("Mode", value.Mode.ToString());
        writer.WriteString("Animation", value.Animation ?? EffectAnimations.Solid);
        if (value.WledFxId is int fxId)
            writer.WriteNumber("WledFxId", fxId);
        if (value.WledOptions is WledPresetOptions presetOptions)
        {
            writer.WritePropertyName("WledOptions");
            JsonSerializer.Serialize(writer, presetOptions, options);
        }

        writer.WriteEndObject();
    }

    private static bool IsLegacyRgb(JsonElement root)
    {
        var hasTopLevelRgb = HasProperty(root, "R") || HasProperty(root, "G") || HasProperty(root, "B");
        if (!hasTopLevelRgb)
            return false;

        // New shape nests RGB under Color; never treat that as legacy.
        return !HasProperty(root, "Color") && !HasProperty(root, "Mode") && !HasProperty(root, "Animation");
    }

    private static EffectSlot ReadEffectSlot(JsonElement root)
    {
        var slot = new EffectSlot();

        if (TryGetProperty(root, "Color", out var colorElement))
            slot.Color = DeserializeColor(colorElement);

        if (TryGetProperty(root, "Mode", out var modeElement) &&
            modeElement.ValueKind == JsonValueKind.String &&
            Enum.TryParse<EffectMode>(modeElement.GetString(), ignoreCase: true, out var mode))
            slot.Mode = mode;

        if (TryGetProperty(root, "Animation", out var animationElement) &&
            animationElement.ValueKind == JsonValueKind.String)
            slot.Animation = animationElement.GetString() ?? EffectAnimations.Solid;

        if (TryGetProperty(root, "WledFxId", out var fxElement) &&
            fxElement.ValueKind == JsonValueKind.Number &&
            fxElement.TryGetInt32(out var fxId))
            slot.WledFxId = fxId;

        if (TryGetProperty(root, "WledOptions", out var optionsElement) &&
            optionsElement.ValueKind == JsonValueKind.Object)
            slot.WledOptions = ReadPresetOptions(optionsElement);

        return slot;
    }

    private static WledPresetOptions ReadPresetOptions(JsonElement root)
    {
        var options = new WledPresetOptions();
        if (TryGetInt(root, "Speed", out var speed))
            options.Speed = speed;
        if (TryGetInt(root, "Intensity", out var intensity))
            options.Intensity = intensity;
        if (TryGetInt(root, "PaletteId", out var paletteId))
            options.PaletteId = paletteId;
        if (TryGetProperty(root, "Overlay", out var overlayElement) &&
            (overlayElement.ValueKind == JsonValueKind.True || overlayElement.ValueKind == JsonValueKind.False))
            options.Overlay = overlayElement.GetBoolean();
        return options;
    }

    private static RgbColor DeserializeColor(JsonElement element)
    {
        var color = new RgbColor();
        if (TryGetByte(element, "R", out var r))
            color.R = r;
        if (TryGetByte(element, "G", out var g))
            color.G = g;
        if (TryGetByte(element, "B", out var b))
            color.B = b;
        return color;
    }

    private static bool TryGetByte(JsonElement parent, string name, out byte value)
    {
        value = 0;
        if (!TryGetInt(parent, name, out var n))
            return false;
        value = (byte)Math.Clamp(n, 0, 255);
        return true;
    }

    private static bool TryGetInt(JsonElement parent, string name, out int value)
    {
        value = 0;
        if (!TryGetProperty(parent, name, out var element))
            return false;
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var n))
            return false;
        value = n;
        return true;
    }

    private static bool HasProperty(JsonElement element, string name) =>
        TryGetProperty(element, name, out _);

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
