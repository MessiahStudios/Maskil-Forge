using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public static class TimelineResolution
{
    public const int TicksPerQuarterNote = 480;
}

public readonly record struct MusicalPosition
{
    [JsonConstructor]
    public MusicalPosition(int bar, int beat, int tick)
    {
        if (bar < 1) throw new ArgumentOutOfRangeException(nameof(bar), "Bar numbers begin at 1.");
        if (beat < 1) throw new ArgumentOutOfRangeException(nameof(beat), "Beat numbers begin at 1.");
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), "Tick cannot be negative.");
        Bar = bar;
        Beat = beat;
        Tick = tick;
    }

    public int Bar { get; }
    public int Beat { get; }
    public int Tick { get; }
}

/// <summary>
/// A musical coordinate relative to the beginning of its owning song section.
/// </summary>
public readonly record struct BeatPosition : IComparable<BeatPosition>
{
    [JsonConstructor]
    public BeatPosition(int bar, int beat, int tick)
    {
        if (bar < 1) throw new ArgumentOutOfRangeException(nameof(bar), "Section-relative bar numbers begin at 1.");
        if (beat < 1) throw new ArgumentOutOfRangeException(nameof(beat), "Beat numbers begin at 1.");
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), "Tick cannot be negative.");
        Bar = bar;
        Beat = beat;
        Tick = tick;
    }

    public int Bar { get; }
    public int Beat { get; }
    public int Tick { get; }

    public int CompareTo(BeatPosition other)
    {
        var barComparison = Bar.CompareTo(other.Bar);
        if (barComparison != 0) return barComparison;
        var beatComparison = Beat.CompareTo(other.Beat);
        return beatComparison != 0 ? beatComparison : Tick.CompareTo(other.Tick);
    }
}

public enum PlacementProvenance
{
    Manual,
    Analyzer,
    Imported
}

/// <summary>
/// An artist-authoritative anchor connecting one stable syllable to section-relative musical time.
/// </summary>
public sealed class SyllablePlacement
{
    [JsonConstructor]
    public SyllablePlacement(
        SyllablePlacementId id,
        SyllableId syllableId,
        BeatPosition position,
        PlacementProvenance provenance)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A syllable placement ID is required.", nameof(id));
        if (syllableId.Value == Guid.Empty) throw new ArgumentException("A syllable ID is required.", nameof(syllableId));
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance), "Placement provenance is invalid.");
        Id = id;
        SyllableId = syllableId;
        Position = position;
        Provenance = provenance;
    }

    public SyllablePlacementId Id { get; }
    public SyllableId SyllableId { get; }
    public BeatPosition Position { get; }
    public PlacementProvenance Provenance { get; }
}

public sealed record TempoEvent
{
    public TempoEvent(int beat, decimal beatsPerMinute)
    {
        if (beat < 0) throw new ArgumentOutOfRangeException(nameof(beat), "Beat cannot be negative.");
        if (beatsPerMinute is < 20 or > 300) throw new ArgumentOutOfRangeException(nameof(beatsPerMinute), "Tempo must be between 20 and 300 BPM.");
        Beat = beat;
        BeatsPerMinute = beatsPerMinute;
    }

    public int Beat { get; }
    public decimal BeatsPerMinute { get; }
}

public sealed record TimeSignatureEvent
{
    private static readonly int[] ValidDenominators = [1, 2, 4, 8, 16, 32];

    public TimeSignatureEvent(int beat, int numerator, int denominator)
    {
        if (beat < 0) throw new ArgumentOutOfRangeException(nameof(beat), "Beat cannot be negative.");
        if (numerator is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(numerator), "Numerator must be between 1 and 32.");
        if (!ValidDenominators.Contains(denominator)) throw new ArgumentOutOfRangeException(nameof(denominator), "Denominator must be 1, 2, 4, 8, 16, or 32.");
        Beat = beat;
        Numerator = numerator;
        Denominator = denominator;
    }

    public int Beat { get; }
    public int Numerator { get; }
    public int Denominator { get; }
}

public sealed class TempoMap
{
    private readonly List<TempoEvent> _events;

    [JsonConstructor]
    public TempoMap(IReadOnlyList<TempoEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _events = events.ToList();
        if (_events.Count != 1 || _events[0].Beat != 0)
            throw new ArgumentException("The current timeline supports exactly one tempo event at beat 0.", nameof(events));
    }

    public IReadOnlyList<TempoEvent> Events => _events;
    public void SetInitialTempo(decimal beatsPerMinute) => _events[0] = new TempoEvent(0, beatsPerMinute);
}

public sealed class TimeSignatureMap
{
    private readonly List<TimeSignatureEvent> _events;

    [JsonConstructor]
    public TimeSignatureMap(IReadOnlyList<TimeSignatureEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _events = events.ToList();
        if (_events.Count != 1 || _events[0].Beat != 0)
            throw new ArgumentException("The current timeline supports exactly one time-signature event at beat 0.", nameof(events));
    }

    public IReadOnlyList<TimeSignatureEvent> Events => _events;
    public void SetInitialTimeSignature(int numerator, int denominator) =>
        _events[0] = new TimeSignatureEvent(0, numerator, denominator);
}

public sealed record SectionPlacement
{
    [JsonConstructor]
    public SectionPlacement(SectionId sectionId, MusicalPosition start, int durationBars)
    {
        if (sectionId.Value == Guid.Empty) throw new ArgumentException("A section ID is required.", nameof(sectionId));
        if (start.Beat != 1 || start.Tick != 0)
            throw new ArgumentException("Sections currently begin at the start of a bar.", nameof(start));
        if (durationBars is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(durationBars), "Section duration must be between 1 and 128 bars.");
        SectionId = sectionId;
        Start = start;
        DurationBars = durationBars;
    }

    public SectionId SectionId { get; }
    public MusicalPosition Start { get; }
    public int DurationBars { get; }
    [JsonIgnore] public int EndBarExclusive => Start.Bar + DurationBars;
}

public sealed class SongTimeline
{
    private readonly List<SectionPlacement> _sectionPlacements;

    [JsonConstructor]
    public SongTimeline(
        int ticksPerQuarterNote,
        TempoMap tempoMap,
        TimeSignatureMap timeSignatureMap,
        IReadOnlyList<SectionPlacement>? sectionPlacements = null)
    {
        if (ticksPerQuarterNote != TimelineResolution.TicksPerQuarterNote)
            throw new ArgumentOutOfRangeException(nameof(ticksPerQuarterNote), $"Timeline resolution must be {TimelineResolution.TicksPerQuarterNote} PPQ.");
        TicksPerQuarterNote = ticksPerQuarterNote;
        TempoMap = tempoMap ?? throw new ArgumentNullException(nameof(tempoMap));
        TimeSignatureMap = timeSignatureMap ?? throw new ArgumentNullException(nameof(timeSignatureMap));
        _sectionPlacements = sectionPlacements?.ToList() ?? [];
        if (_sectionPlacements.Select(item => item.SectionId).Distinct().Count() != _sectionPlacements.Count)
            throw new ArgumentException("Section placement IDs must be unique.", nameof(sectionPlacements));
    }

    public int TicksPerQuarterNote { get; }
    public TempoMap TempoMap { get; }
    public TimeSignatureMap TimeSignatureMap { get; }
    public IReadOnlyList<SectionPlacement> SectionPlacements => _sectionPlacements;

    public static SongTimeline CreateDefault() => new(
        TimelineResolution.TicksPerQuarterNote,
        new TempoMap([new TempoEvent(0, 120)]),
        new TimeSignatureMap([new TimeSignatureEvent(0, 4, 4)]));

    public long ToAbsoluteTicks(MusicalPosition position)
    {
        var meter = TimeSignatureMap.Events[0];
        var ticksPerBeat = TicksPerBeat(meter);
        if (position.Beat > meter.Numerator)
            throw new ArgumentOutOfRangeException(nameof(position), $"Beat must be between 1 and {meter.Numerator}.");
        if (position.Tick >= ticksPerBeat)
            throw new ArgumentOutOfRangeException(nameof(position), $"Tick must be between 0 and {ticksPerBeat - 1} for this meter.");
        return ((long)(position.Bar - 1) * meter.Numerator * ticksPerBeat)
            + ((long)(position.Beat - 1) * ticksPerBeat)
            + position.Tick;
    }

    public MusicalPosition FromAbsoluteTicks(long absoluteTicks)
    {
        if (absoluteTicks < 0) throw new ArgumentOutOfRangeException(nameof(absoluteTicks));
        var meter = TimeSignatureMap.Events[0];
        var ticksPerBeat = TicksPerBeat(meter);
        var ticksPerBar = (long)meter.Numerator * ticksPerBeat;
        var bar = (int)(absoluteTicks / ticksPerBar) + 1;
        var withinBar = absoluteTicks % ticksPerBar;
        var beat = (int)(withinBar / ticksPerBeat) + 1;
        var tick = (int)(withinBar % ticksPerBeat);
        return new MusicalPosition(bar, beat, tick);
    }

    public SectionPlacement FindSection(SectionId sectionId) =>
        _sectionPlacements.SingleOrDefault(item => item.SectionId == sectionId)
        ?? throw new KeyNotFoundException($"Timeline placement for section '{sectionId}' was not found.");

    public void SetSectionDuration(SectionId sectionId, int durationBars, IReadOnlyList<SectionId> orderedSectionIds)
    {
        if (durationBars < 1 || durationBars > 128)
            throw new ArgumentOutOfRangeException(nameof(durationBars), "Section duration must be between 1 and 128 bars.");
        _ = FindSection(sectionId);
        ReflowSections(orderedSectionIds, new Dictionary<SectionId, int> { [sectionId] = durationBars });
    }

    public void ReflowSections(IReadOnlyList<SectionId> orderedSectionIds, IReadOnlyDictionary<SectionId, int>? durationOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(orderedSectionIds);
        var durations = _sectionPlacements.ToDictionary(item => item.SectionId, item => item.DurationBars);
        if (durationOverrides is not null)
            foreach (var pair in durationOverrides) durations[pair.Key] = pair.Value;
        _sectionPlacements.Clear();
        var startBar = 1;
        foreach (var sectionId in orderedSectionIds)
        {
            var duration = durations.GetValueOrDefault(sectionId, 8);
            _sectionPlacements.Add(new SectionPlacement(sectionId, new MusicalPosition(startBar, 1, 0), duration));
            startBar += duration;
        }
    }

    public void ValidateSectionOrder(IReadOnlyList<SectionId> orderedSectionIds)
    {
        if (!_sectionPlacements.Select(item => item.SectionId).SequenceEqual(orderedSectionIds))
            throw new ArgumentException("Timeline section placements must match the Song Graph section order.");
        var expectedBar = 1;
        foreach (var placement in _sectionPlacements)
        {
            if (placement.Start.Bar != expectedBar)
                throw new ArgumentException("Timeline section placements must be contiguous and ordered.");
            expectedBar = placement.EndBarExclusive;
        }
    }

    private int TicksPerBeat(TimeSignatureEvent meter) => checked(TicksPerQuarterNote * 4 / meter.Denominator);
}
