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

[JsonConverter(typeof(SectionArrangementIdJsonConverter))]
public readonly record struct SectionArrangementId(Guid Value)
{
    public static SectionArrangementId New() => new(Guid.NewGuid());
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

[JsonConverter(typeof(ProsodicPatternIdJsonConverter))]
public readonly record struct ProsodicPatternId(Guid Value)
{
    public static ProsodicPatternId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(ProsodicUnitIdJsonConverter))]
public readonly record struct ProsodicUnitId(Guid Value)
{
    public static ProsodicUnitId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(SyllablePlacementIdJsonConverter))]
public readonly record struct SyllablePlacementId(Guid Value)
{
    public static SyllablePlacementId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(RhythmCandidateIdJsonConverter))]
public readonly record struct RhythmCandidateId(Guid Value)
{
    public static RhythmCandidateId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(RhythmCandidateEventIdJsonConverter))]
public readonly record struct RhythmCandidateEventId(Guid Value)
{
    public static RhythmCandidateEventId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(BreathPointIdJsonConverter))]
public readonly record struct BreathPointId(Guid Value)
{
    public static BreathPointId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(CreativeLockIdJsonConverter))]
public readonly record struct CreativeLockId(Guid Value)
{
    public static CreativeLockId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(HarmonyChordIdJsonConverter))]
public readonly record struct HarmonyChordId(Guid Value)
{
    public static HarmonyChordId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(ChordVoicingIdJsonConverter))]
public readonly record struct ChordVoicingId(Guid Value)
{
    public static ChordVoicingId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(ChordVoiceIdJsonConverter))]
public readonly record struct ChordVoiceId(Guid Value)
{
    public static ChordVoiceId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(HarmonyCandidateIdJsonConverter))]
public readonly record struct HarmonyCandidateId(Guid Value)
{
    public static HarmonyCandidateId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(HarmonyCandidateEventIdJsonConverter))]
public readonly record struct HarmonyCandidateEventId(Guid Value)
{
    public static HarmonyCandidateEventId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(SchemaVersionJsonConverter))]
public readonly record struct SchemaVersion(int Value)
{
    public static SchemaVersion Current => new(16);
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

internal sealed class SectionArrangementIdJsonConverter : GuidIdJsonConverter<SectionArrangementId>
{
    protected override SectionArrangementId Create(Guid value) => new(value);
    protected override Guid GetValue(SectionArrangementId value) => value.Value;
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

internal sealed class ProsodicPatternIdJsonConverter : GuidIdJsonConverter<ProsodicPatternId>
{
    protected override ProsodicPatternId Create(Guid value) => new(value);
    protected override Guid GetValue(ProsodicPatternId value) => value.Value;
}

internal sealed class ProsodicUnitIdJsonConverter : GuidIdJsonConverter<ProsodicUnitId>
{
    protected override ProsodicUnitId Create(Guid value) => new(value);
    protected override Guid GetValue(ProsodicUnitId value) => value.Value;
}

internal sealed class SyllablePlacementIdJsonConverter : GuidIdJsonConverter<SyllablePlacementId>
{
    protected override SyllablePlacementId Create(Guid value) => new(value);
    protected override Guid GetValue(SyllablePlacementId value) => value.Value;
}

internal sealed class RhythmCandidateIdJsonConverter : GuidIdJsonConverter<RhythmCandidateId>
{
    protected override RhythmCandidateId Create(Guid value) => new(value);
    protected override Guid GetValue(RhythmCandidateId value) => value.Value;
}

internal sealed class RhythmCandidateEventIdJsonConverter : GuidIdJsonConverter<RhythmCandidateEventId>
{
    protected override RhythmCandidateEventId Create(Guid value) => new(value);
    protected override Guid GetValue(RhythmCandidateEventId value) => value.Value;
}

internal sealed class BreathPointIdJsonConverter : GuidIdJsonConverter<BreathPointId>
{
    protected override BreathPointId Create(Guid value) => new(value);
    protected override Guid GetValue(BreathPointId value) => value.Value;
}

internal sealed class CreativeLockIdJsonConverter : GuidIdJsonConverter<CreativeLockId>
{
    protected override CreativeLockId Create(Guid value) => new(value);
    protected override Guid GetValue(CreativeLockId value) => value.Value;
}

internal sealed class HarmonyChordIdJsonConverter : GuidIdJsonConverter<HarmonyChordId>
{
    protected override HarmonyChordId Create(Guid value) => new(value);
    protected override Guid GetValue(HarmonyChordId value) => value.Value;
}

internal sealed class ChordVoicingIdJsonConverter : GuidIdJsonConverter<ChordVoicingId>
{
    protected override ChordVoicingId Create(Guid value) => new(value);
    protected override Guid GetValue(ChordVoicingId value) => value.Value;
}

internal sealed class ChordVoiceIdJsonConverter : GuidIdJsonConverter<ChordVoiceId>
{
    protected override ChordVoiceId Create(Guid value) => new(value);
    protected override Guid GetValue(ChordVoiceId value) => value.Value;
}

internal sealed class HarmonyCandidateIdJsonConverter : GuidIdJsonConverter<HarmonyCandidateId>
{
    protected override HarmonyCandidateId Create(Guid value) => new(value);
    protected override Guid GetValue(HarmonyCandidateId value) => value.Value;
}

internal sealed class HarmonyCandidateEventIdJsonConverter : GuidIdJsonConverter<HarmonyCandidateEventId>
{
    protected override HarmonyCandidateEventId Create(Guid value) => new(value);
    protected override Guid GetValue(HarmonyCandidateEventId value) => value.Value;
}
