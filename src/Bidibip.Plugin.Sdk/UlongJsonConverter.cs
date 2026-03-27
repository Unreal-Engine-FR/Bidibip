using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bidibip.Plugin.Sdk;

/// <summary>
/// Handles ulong values stored as either JSON numbers or JSON strings.
/// Discord IDs exceed double precision, so they're often stored as strings.
/// </summary>
public sealed class UlongJsonConverter : JsonConverter<ulong>
{
    public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetUInt64(),
            JsonTokenType.String => ulong.TryParse(reader.GetString(), out var v) ? v : 0,
            _ => 0
        };
    }

    public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Pre-configured JsonSerializerOptions with UlongJsonConverter and indented output.
/// </summary>
public static class PluginJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        Converters = { new UlongJsonConverter() }
    };
}
