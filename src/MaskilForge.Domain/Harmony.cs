using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum HarmonyProvenance
{
    Manual,
    Analyzer,
    Imported
}

/// <summary>
/// An artist-authored chord placed in section-relative musical time.
/// Progressions are ordered lists of these events; generation and audition are later slices.
/// </summary>
public sealed class HarmonyChord
{
    [JsonConstructor]
    public HarmonyChord(
        HarmonyChordId id,
        ChordSymbol chord,
        BeatPosition start,
        int durationBars,
        HarmonyProvenance provenance)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A harmony chord ID is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(chord);
        if (durationBars < 1 || durationBars > 128)
            throw new ArgumentOutOfRangeException(nameof(durationBars), "Harmony chord duration must be between 1 and 128 bars.");
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance), "Harmony provenance is invalid.");

        Id = id;
        Chord = chord;
        Start = start;
        DurationBars = durationBars;
        Provenance = provenance;
    }

    public HarmonyChordId Id { get; }
    public ChordSymbol Chord { get; }
    public BeatPosition Start { get; }
    public int DurationBars { get; }
    public HarmonyProvenance Provenance { get; }

    public static HarmonyChord Create(ChordSymbol chord, BeatPosition start, int durationBars = 1) =>
        new(HarmonyChordId.New(), chord, start, durationBars, HarmonyProvenance.Manual);

    public HarmonyChord With(
        ChordSymbol? chord = null,
        BeatPosition? start = null,
        int? durationBars = null) =>
        new(Id, chord ?? Chord, start ?? Start, durationBars ?? DurationBars, Provenance);
}

public sealed class HarmonyCandidateEvent
{
    [JsonConstructor]
    public HarmonyCandidateEvent(
        HarmonyCandidateEventId id,
        int position,
        ChordSymbol chord,
        BeatPosition start,
        int durationBars)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A harmony candidate event ID is required.", nameof(id));
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        ArgumentNullException.ThrowIfNull(chord);
        if (durationBars < 1 || durationBars > 128)
            throw new ArgumentOutOfRangeException(nameof(durationBars), "Harmony candidate event duration must be between 1 and 128 bars.");
        Id = id;
        Position = position;
        Chord = chord;
        Start = start;
        DurationBars = durationBars;
    }

    public HarmonyCandidateEventId Id { get; }
    public int Position { get; }
    public ChordSymbol Chord { get; }
    public BeatPosition Start { get; }
    public int DurationBars { get; }
}

/// <summary>A named, durable alternative to a section's authoritative harmony progression.</summary>
public sealed class HarmonyCandidate
{
    private readonly List<HarmonyCandidateEvent> _events;

    [JsonConstructor]
    public HarmonyCandidate(
        HarmonyCandidateId id,
        string label,
        HarmonyProvenance provenance,
        IReadOnlyList<HarmonyCandidateEvent> events)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A harmony candidate ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A harmony candidate label is required.", nameof(label));
        if (label.Trim().Length > 100) throw new ArgumentOutOfRangeException(nameof(label), "A harmony candidate label cannot exceed 100 characters.");
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance), "Harmony provenance is invalid.");
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) throw new ArgumentException("A harmony candidate must contain at least one chord.", nameof(events));
        if (events.Select(item => item.Id).Distinct().Count() != events.Count)
            throw new ArgumentException("Harmony candidate event IDs must be unique.", nameof(events));
        if (!events.Select(item => item.Position).SequenceEqual(Enumerable.Range(0, events.Count)))
            throw new ArgumentException("Harmony candidate event positions must be contiguous and ordered from zero.", nameof(events));
        if (!events.Select(item => item.Start).SequenceEqual(events.Select(item => item.Start).OrderBy(item => item)))
            throw new ArgumentException("Harmony candidate events must advance through musical time.", nameof(events));

        Id = id;
        Label = label.Trim();
        Provenance = provenance;
        _events = events.ToList();
    }

    public HarmonyCandidateId Id { get; }
    public string Label { get; }
    public HarmonyProvenance Provenance { get; }
    public IReadOnlyList<HarmonyCandidateEvent> Events => _events;

    public HarmonyCandidate WithLabel(string label) => new(Id, label, Provenance, _events);
}
