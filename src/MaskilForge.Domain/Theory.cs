using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum NoteLetter
{
    C,
    D,
    E,
    F,
    G,
    A,
    B
}

public enum Accidental
{
    Natural,
    Sharp,
    Flat
}

public enum ScaleMode
{
    Major,
    NaturalMinor
}

public enum ChordQuality
{
    Major,
    Minor,
    Diminished,
    Augmented,
    DominantSeventh
}

/// <summary>Pitch class in equal temperament, 0 = C … 11 = B.</summary>
public readonly record struct PitchClass
{
    public PitchClass(int value)
    {
        if (value is < 0 or > 11)
            throw new ArgumentOutOfRangeException(nameof(value), "Pitch class must be between 0 and 11.");
        Value = value;
    }

    public int Value { get; }

    public PitchClass Transpose(int semitones) => new(Mod12(Value + semitones));

    public override string ToString() => Value.ToString();

    private static int Mod12(int value)
    {
        var mod = value % 12;
        return mod < 0 ? mod + 12 : mod;
    }
}

public sealed class PitchSpelling
{
    private static readonly int[] NaturalPitchClasses = [0, 2, 4, 5, 7, 9, 11];

    [JsonConstructor]
    public PitchSpelling(NoteLetter letter, Accidental accidental = Accidental.Natural)
    {
        if (!Enum.IsDefined(letter)) throw new ArgumentOutOfRangeException(nameof(letter));
        if (!Enum.IsDefined(accidental)) throw new ArgumentOutOfRangeException(nameof(accidental));
        Letter = letter;
        Accidental = accidental;
    }

    public NoteLetter Letter { get; }
    public Accidental Accidental { get; }

    [JsonIgnore]
    public PitchClass PitchClass
    {
        get
        {
            var natural = NaturalPitchClasses[(int)Letter];
            var offset = Accidental switch
            {
                Accidental.Natural => 0,
                Accidental.Sharp => 1,
                Accidental.Flat => -1,
                _ => throw new InvalidOperationException("Accidental is invalid.")
            };
            return new PitchClass((natural + offset + 12) % 12);
        }
    }

    public string ToDisplayString() => Accidental switch
    {
        Accidental.Natural => Letter.ToString(),
        Accidental.Sharp => $"{Letter}#",
        Accidental.Flat => $"{Letter}b",
        _ => Letter.ToString()
    };

    public override string ToString() => ToDisplayString();
}

public sealed class MusicalKey
{
    public static MusicalKey Default { get; } = new(NoteLetter.C, Accidental.Natural, ScaleMode.Major);

    [JsonConstructor]
    public MusicalKey(NoteLetter tonic, Accidental accidental = Accidental.Natural, ScaleMode mode = ScaleMode.Major)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        Tonic = tonic;
        Accidental = accidental;
        Mode = mode;
        Spelling = new PitchSpelling(tonic, accidental);
    }

    public NoteLetter Tonic { get; }
    public Accidental Accidental { get; }
    public ScaleMode Mode { get; }

    [JsonIgnore]
    public PitchSpelling Spelling { get; }

    [JsonIgnore]
    public PitchClass TonicPitchClass => Spelling.PitchClass;

    [JsonIgnore]
    public IReadOnlyList<PitchClass> ScalePitchClasses => Theory.ScalePitchClasses(TonicPitchClass, Mode);

    public MusicalKey Transpose(int semitones) =>
        Theory.KeyFromPitchClass(TonicPitchClass.Transpose(semitones), Mode);

    public string ToDisplayString()
    {
        var modeLabel = Mode == ScaleMode.Major ? "major" : "natural minor";
        return $"{Spelling.ToDisplayString()} {modeLabel}";
    }

    public override string ToString() => ToDisplayString();
}

public sealed class ChordSymbol
{
    [JsonConstructor]
    public ChordSymbol(NoteLetter root, Accidental accidental = Accidental.Natural, ChordQuality quality = ChordQuality.Major)
    {
        if (!Enum.IsDefined(quality)) throw new ArgumentOutOfRangeException(nameof(quality));
        Root = root;
        Accidental = accidental;
        Quality = quality;
        Spelling = new PitchSpelling(root, accidental);
    }

    public NoteLetter Root { get; }
    public Accidental Accidental { get; }
    public ChordQuality Quality { get; }

    [JsonIgnore]
    public PitchSpelling Spelling { get; }

    [JsonIgnore]
    public IReadOnlyList<PitchClass> PitchClasses => Theory.ChordPitchClasses(Spelling.PitchClass, Quality);

    public ChordSymbol Transpose(int semitones) =>
        Theory.ChordFromPitchClass(Spelling.PitchClass.Transpose(semitones), Quality);

    public string ToDisplayString()
    {
        var quality = Quality switch
        {
            ChordQuality.Major => "",
            ChordQuality.Minor => "m",
            ChordQuality.Diminished => "dim",
            ChordQuality.Augmented => "aug",
            ChordQuality.DominantSeventh => "7",
            _ => ""
        };
        return $"{Spelling.ToDisplayString()}{quality}";
    }

    public override string ToString() => ToDisplayString();
}

public static class Theory
{
    private static readonly int[] MajorIntervals = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] NaturalMinorIntervals = [0, 2, 3, 5, 7, 8, 10];

    private static readonly PitchSpelling[] SharpSpellings =
    [
        new(NoteLetter.C),
        new(NoteLetter.C, Accidental.Sharp),
        new(NoteLetter.D),
        new(NoteLetter.D, Accidental.Sharp),
        new(NoteLetter.E),
        new(NoteLetter.F),
        new(NoteLetter.F, Accidental.Sharp),
        new(NoteLetter.G),
        new(NoteLetter.G, Accidental.Sharp),
        new(NoteLetter.A),
        new(NoteLetter.A, Accidental.Sharp),
        new(NoteLetter.B)
    ];

    private static readonly PitchSpelling[] FlatSpellings =
    [
        new(NoteLetter.C),
        new(NoteLetter.D, Accidental.Flat),
        new(NoteLetter.D),
        new(NoteLetter.E, Accidental.Flat),
        new(NoteLetter.E),
        new(NoteLetter.F),
        new(NoteLetter.G, Accidental.Flat),
        new(NoteLetter.G),
        new(NoteLetter.A, Accidental.Flat),
        new(NoteLetter.A),
        new(NoteLetter.B, Accidental.Flat),
        new(NoteLetter.B)
    ];

    public static IReadOnlyList<int> ScaleIntervals(ScaleMode mode) => mode switch
    {
        ScaleMode.Major => MajorIntervals,
        ScaleMode.NaturalMinor => NaturalMinorIntervals,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public static IReadOnlyList<PitchClass> ScalePitchClasses(PitchClass tonic, ScaleMode mode) =>
        ScaleIntervals(mode).Select(interval => tonic.Transpose(interval)).ToArray();

    public static bool IsScaleTone(PitchClass pitch, PitchClass tonic, ScaleMode mode) =>
        ScalePitchClasses(tonic, mode).Any(tone => tone.Value == pitch.Value);

    public static IReadOnlyList<int> ChordIntervals(ChordQuality quality) => quality switch
    {
        ChordQuality.Major => [0, 4, 7],
        ChordQuality.Minor => [0, 3, 7],
        ChordQuality.Diminished => [0, 3, 6],
        ChordQuality.Augmented => [0, 4, 8],
        ChordQuality.DominantSeventh => [0, 4, 7, 10],
        _ => throw new ArgumentOutOfRangeException(nameof(quality))
    };

    public static IReadOnlyList<PitchClass> ChordPitchClasses(PitchClass root, ChordQuality quality) =>
        ChordIntervals(quality).Select(interval => root.Transpose(interval)).ToArray();

    public static int IntervalSemitones(PitchClass from, PitchClass to)
    {
        var delta = to.Value - from.Value;
        return delta < 0 ? delta + 12 : delta;
    }

    public static MusicalKey KeyFromPitchClass(PitchClass tonic, ScaleMode mode, bool preferFlats = false)
    {
        var spelling = PreferSpelling(tonic, preferFlats);
        return new MusicalKey(spelling.Letter, spelling.Accidental, mode);
    }

    public static ChordSymbol ChordFromPitchClass(PitchClass root, ChordQuality quality, bool preferFlats = false)
    {
        var spelling = PreferSpelling(root, preferFlats);
        return new ChordSymbol(spelling.Letter, spelling.Accidental, quality);
    }

    public static PitchSpelling PreferSpelling(PitchClass pitch, bool preferFlats = false) =>
        (preferFlats ? FlatSpellings : SharpSpellings)[pitch.Value];

    /// <summary>
    /// Derived Roman-numeral label relative to the song key. Returns null when the chord is not a simple diatonic match.
    /// </summary>
    public static string? RomanNumeral(MusicalKey key, ChordSymbol chord)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(chord);
        var scale = ScalePitchClasses(key.TonicPitchClass, key.Mode);
        var degree = scale.Select((tone, index) => (tone, index))
            .Where(item => item.tone.Value == chord.Spelling.PitchClass.Value)
            .Select(item => item.index)
            .Cast<int?>()
            .FirstOrDefault();
        if (degree is null) return null;

        var expectedQuality = key.Mode == ScaleMode.Major
            ? MajorDiatonicQualities[degree.Value]
            : MinorDiatonicQualities[degree.Value];
        if (chord.Quality != expectedQuality
            && !(expectedQuality == ChordQuality.Major
                 && chord.Quality == ChordQuality.DominantSeventh
                 && degree.Value == 4))
            return null;

        var numerals = key.Mode == ScaleMode.Major
            ? new[] { "I", "ii", "iii", "IV", "V", "vi", "vii°" }
            : new[] { "i", "ii°", "III", "iv", "v", "VI", "VII" };
        var label = numerals[degree.Value];
        if (chord.Quality == ChordQuality.DominantSeventh)
            return degree.Value == 4 && key.Mode == ScaleMode.Major ? "V7" : $"{label}7";
        return label;
    }

    private static readonly ChordQuality[] MajorDiatonicQualities =
    [
        ChordQuality.Major,
        ChordQuality.Minor,
        ChordQuality.Minor,
        ChordQuality.Major,
        ChordQuality.Major,
        ChordQuality.Minor,
        ChordQuality.Diminished
    ];

    private static readonly ChordQuality[] MinorDiatonicQualities =
    [
        ChordQuality.Minor,
        ChordQuality.Diminished,
        ChordQuality.Major,
        ChordQuality.Minor,
        ChordQuality.Minor,
        ChordQuality.Major,
        ChordQuality.Major
    ];
}
