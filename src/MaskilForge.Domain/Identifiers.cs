using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

[JsonConverter(typeof(ProjectIdJsonConverter))]
public readonly record struct ProjectId(Guid Value)
{
    public static ProjectId New() => new(Guid.NewGuid());
    public static bool TryParse(string? value, out ProjectId id)
    {
        var parsed = Guid.TryParse(value, out var guid);
        id = parsed ? new ProjectId(guid) : default;
        return parsed;
    }
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(SectionIdJsonConverter))]
public readonly record struct SectionId(Guid Value)
{
    public static SectionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(TrackIdJsonConverter))]
public readonly record struct TrackId(Guid Value)
{
    public static TrackId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(ClipIdJsonConverter))]
public readonly record struct ClipId(Guid Value)
{
    public static ClipId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(LyricLineIdJsonConverter))]
public readonly record struct LyricLineId(Guid Value)
{
    public static LyricLineId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(LyricWordIdJsonConverter))]
public readonly record struct LyricWordId(Guid Value)
{
    public static LyricWordId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(SyllableIdJsonConverter))]
public readonly record struct SyllableId(Guid Value)
{
    public static SyllableId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(LyricPhraseIdJsonConverter))]
public readonly record struct LyricPhraseId(Guid Value)
{
    public static LyricPhraseId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(PunctuationIdJsonConverter))]
public readonly record struct PunctuationId(Guid Value)
{
    public static PunctuationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(SchemaVersionJsonConverter))]
public readonly record struct SchemaVersion(int Value)
{
    public static SchemaVersion Current => new(5);
}

internal sealed class SchemaVersionJsonConverter : JsonConverter<SchemaVersion>
{
    public override SchemaVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number) return new SchemaVersion(reader.GetInt32());
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Schema version must be a number.");
        using var document = JsonDocument.ParseValue(ref reader);
        if (!document.RootElement.TryGetProperty("value", out var value) || !value.TryGetInt32(out var version))
            throw new JsonException("Schema version object must contain a numeric value.");
        return new SchemaVersion(version);
    }

    public override void Write(Utf8JsonWriter writer, SchemaVersion value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}

internal abstract class GuidIdJsonConverter<T> : JsonConverter<T>
{
    protected abstract T Create(Guid value);
    protected abstract Guid GetValue(T value);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (!Guid.TryParse(text, out var value))
        {
            throw new JsonException($"'{text}' is not a valid identifier.");
        }

        return Create(value);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(GetValue(value));
}

internal sealed class ProjectIdJsonConverter : GuidIdJsonConverter<ProjectId>
{
    protected override ProjectId Create(Guid value) => new(value);
    protected override Guid GetValue(ProjectId value) => value.Value;
}

internal sealed class SectionIdJsonConverter : GuidIdJsonConverter<SectionId>
{
    protected override SectionId Create(Guid value) => new(value);
    protected override Guid GetValue(SectionId value) => value.Value;
}

internal sealed class TrackIdJsonConverter : GuidIdJsonConverter<TrackId>
{
    protected override TrackId Create(Guid value) => new(value);
    protected override Guid GetValue(TrackId value) => value.Value;
}

internal sealed class ClipIdJsonConverter : GuidIdJsonConverter<ClipId>
{
    protected override ClipId Create(Guid value) => new(value);
    protected override Guid GetValue(ClipId value) => value.Value;
}

internal sealed class LyricLineIdJsonConverter : GuidIdJsonConverter<LyricLineId>
{
    protected override LyricLineId Create(Guid value) => new(value);
    protected override Guid GetValue(LyricLineId value) => value.Value;
}

internal sealed class LyricWordIdJsonConverter : GuidIdJsonConverter<LyricWordId>
{
    protected override LyricWordId Create(Guid value) => new(value);
    protected override Guid GetValue(LyricWordId value) => value.Value;
}

internal sealed class SyllableIdJsonConverter : GuidIdJsonConverter<SyllableId>
{
    protected override SyllableId Create(Guid value) => new(value);
    protected override Guid GetValue(SyllableId value) => value.Value;
}

internal sealed class LyricPhraseIdJsonConverter : GuidIdJsonConverter<LyricPhraseId>
{
    protected override LyricPhraseId Create(Guid value) => new(value);
    protected override Guid GetValue(LyricPhraseId value) => value.Value;
}

internal sealed class PunctuationIdJsonConverter : GuidIdJsonConverter<PunctuationId>
{
    protected override PunctuationId Create(Guid value) => new(value);
    protected override Guid GetValue(PunctuationId value) => value.Value;
}
