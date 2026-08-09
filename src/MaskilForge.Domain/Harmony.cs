using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum HarmonyProvenance
{
    Manual,
    Analyzer,
    Imported
}

public sealed class RegisteredPitch
{
    [JsonConstructor]
    public RegisteredPitch(NoteLetter letter, Accidental accidental, int octave)
    {
        if (!Enum.IsDefined(letter)) throw new ArgumentOutOfRangeException(nameof(letter));
        if (!Enum.IsDefined(accidental)) throw new ArgumentOutOfRangeException(nameof(accidental));
        Letter = letter;
        Accidental = accidental;
        Octave = octave;
        if (MidiNumber is < 0 or > 127) throw new ArgumentOutOfRangeException(nameof(octave), "Registered pitch must fit the MIDI note range.");
    }

    public NoteLetter Letter { get; }
    public Accidental Accidental { get; }
    public int Octave { get; }
    [JsonIgnore] public PitchClass PitchClass => new PitchSpelling(Letter, Accidental).PitchClass;
    [JsonIgnore] public int MidiNumber => (Octave + 1) * 12 + PitchClass.Value;
    public string ToDisplayString() => $"{new PitchSpelling(Letter, Accidental).ToDisplayString()}{Octave}";
}

public sealed class ChordVoice
{
    [JsonConstructor]
    public ChordVoice(ChordVoiceId id, int position, RegisteredPitch pitch, HarmonyProvenance provenance)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A chord voice ID is required.", nameof(id));
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        Pitch = pitch ?? throw new ArgumentNullException(nameof(pitch));
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance));
        Id = id; Position = position; Provenance = provenance;
    }
    public ChordVoiceId Id { get; }
    public int Position { get; }
    public RegisteredPitch Pitch { get; }
    public HarmonyProvenance Provenance { get; }
}

public sealed class ChordVoicing
{
    [JsonConstructor]
    public ChordVoicing(ChordVoicingId id, int minimumMidiNote, int maximumMidiNote, IReadOnlyList<ChordVoice> voices)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A chord voicing ID is required.", nameof(id));
        if (minimumMidiNote is < 0 or > 127 || maximumMidiNote is < 0 or > 127 || minimumMidiNote > maximumMidiNote)
            throw new ArgumentOutOfRangeException(nameof(minimumMidiNote), "Voicing register bounds must be ordered MIDI notes between 0 and 127.");
        ArgumentNullException.ThrowIfNull(voices);
        if (voices.Count == 0) throw new ArgumentException("A chord voicing requires at least one voice.", nameof(voices));
        if (!voices.Select(item => item.Position).SequenceEqual(Enumerable.Range(0, voices.Count)))
            throw new ArgumentException("Chord voice positions must be contiguous and ordered from zero.", nameof(voices));
        if (!voices.Select(item => item.Pitch.MidiNumber).SequenceEqual(voices.Select(item => item.Pitch.MidiNumber).OrderBy(item => item)))
            throw new ArgumentException("Chord voices must be ordered from low to high.", nameof(voices));
        if (voices.Select(item => item.Id).Distinct().Count() != voices.Count) throw new ArgumentException("Chord voice IDs must be unique.", nameof(voices));
        if (voices.Any(item => item.Pitch.MidiNumber < minimumMidiNote || item.Pitch.MidiNumber > maximumMidiNote))
            throw new ArgumentException("Every chord voice must fit the configured register bounds.", nameof(voices));
        Id = id; MinimumMidiNote = minimumMidiNote; MaximumMidiNote = maximumMidiNote; Voices = voices.ToList();
    }
    public ChordVoicingId Id { get; }
    public int MinimumMidiNote { get; }
    public int MaximumMidiNote { get; }
    public IReadOnlyList<ChordVoice> Voices { get; }
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
        HarmonyProvenance provenance,
        ChordVoicing? voicing = null)
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
        if (voicing is not null && voicing.Voices.Any(voice => !chord.PitchClasses.Any(tone => tone.Value == voice.Pitch.PitchClass.Value)))
            throw new ArgumentException("Every registered voice must be a tone in the owning chord.", nameof(voicing));
        Voicing = voicing;
    }

    public HarmonyChordId Id { get; }
    public ChordSymbol Chord { get; }
    public BeatPosition Start { get; }
    public int DurationBars { get; }
    public HarmonyProvenance Provenance { get; }
    public ChordVoicing? Voicing { get; }

    public static HarmonyChord Create(ChordSymbol chord, BeatPosition start, int durationBars = 1) =>
        new(HarmonyChordId.New(), chord, start, durationBars, HarmonyProvenance.Manual);

    public HarmonyChord With(
        ChordSymbol? chord = null,
        BeatPosition? start = null,
        int? durationBars = null,
        ChordVoicing? voicing = null,
        bool replaceVoicing = false) =>
        new(Id, chord ?? Chord, start ?? Start, durationBars ?? DurationBars, Provenance, replaceVoicing ? voicing : Voicing);
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
