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
