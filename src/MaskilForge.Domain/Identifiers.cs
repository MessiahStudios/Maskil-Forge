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

public readonly record struct SchemaVersion(int Value)
{
    public static SchemaVersion Current => new(1);
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
