using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// An inspectable Standard MIDI key signature for one stored song key. MIDI
/// only distinguishes major and minor. Spellings outside the conventional
/// circle of fifths have no signature; the host does not invent one.
/// </summary>
public sealed record MidiKeySignature(int SharpsFlats, bool Minor);

public static class MidiKeySignatureMapper
{
    private static readonly Dictionary<(NoteLetter Tonic, Accidental Accidental, ScaleMode Mode), int> Known = new()
    {
        [(NoteLetter.C, Accidental.Natural, ScaleMode.Major)] = 0,
        [(NoteLetter.G, Accidental.Natural, ScaleMode.Major)] = 1,
        [(NoteLetter.D, Accidental.Natural, ScaleMode.Major)] = 2,
        [(NoteLetter.A, Accidental.Natural, ScaleMode.Major)] = 3,
        [(NoteLetter.E, Accidental.Natural, ScaleMode.Major)] = 4,
        [(NoteLetter.B, Accidental.Natural, ScaleMode.Major)] = 5,
        [(NoteLetter.F, Accidental.Sharp, ScaleMode.Major)] = 6,
        [(NoteLetter.C, Accidental.Sharp, ScaleMode.Major)] = 7,
        [(NoteLetter.F, Accidental.Natural, ScaleMode.Major)] = -1,
        [(NoteLetter.B, Accidental.Flat, ScaleMode.Major)] = -2,
        [(NoteLetter.E, Accidental.Flat, ScaleMode.Major)] = -3,
        [(NoteLetter.A, Accidental.Flat, ScaleMode.Major)] = -4,
        [(NoteLetter.D, Accidental.Flat, ScaleMode.Major)] = -5,
        [(NoteLetter.G, Accidental.Flat, ScaleMode.Major)] = -6,
        [(NoteLetter.C, Accidental.Flat, ScaleMode.Major)] = -7,
        [(NoteLetter.A, Accidental.Natural, ScaleMode.NaturalMinor)] = 0,
        [(NoteLetter.E, Accidental.Natural, ScaleMode.NaturalMinor)] = 1,
        [(NoteLetter.B, Accidental.Natural, ScaleMode.NaturalMinor)] = 2,
        [(NoteLetter.F, Accidental.Sharp, ScaleMode.NaturalMinor)] = 3,
        [(NoteLetter.C, Accidental.Sharp, ScaleMode.NaturalMinor)] = 4,
        [(NoteLetter.G, Accidental.Sharp, ScaleMode.NaturalMinor)] = 5,
        [(NoteLetter.D, Accidental.Sharp, ScaleMode.NaturalMinor)] = 6,
        [(NoteLetter.A, Accidental.Sharp, ScaleMode.NaturalMinor)] = 7,
        [(NoteLetter.D, Accidental.Natural, ScaleMode.NaturalMinor)] = -1,
        [(NoteLetter.G, Accidental.Natural, ScaleMode.NaturalMinor)] = -2,
        [(NoteLetter.C, Accidental.Natural, ScaleMode.NaturalMinor)] = -3,
        [(NoteLetter.F, Accidental.Natural, ScaleMode.NaturalMinor)] = -4,
        [(NoteLetter.B, Accidental.Flat, ScaleMode.NaturalMinor)] = -5,
        [(NoteLetter.E, Accidental.Flat, ScaleMode.NaturalMinor)] = -6,
        [(NoteLetter.A, Accidental.Flat, ScaleMode.NaturalMinor)] = -7,
    };

    public static MidiKeySignature? Map(MusicalKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!Known.TryGetValue((key.Tonic, key.Accidental, key.Mode), out var sharpsFlats))
            return null;

        return new MidiKeySignature(sharpsFlats, key.Mode == ScaleMode.NaturalMinor);
    }

    public static byte[] MetaMessage(MidiKeySignature signature) =>
    [
        0xFF,
        0x59,
        0x02,
        unchecked((byte)signature.SharpsFlats),
        signature.Minor ? (byte)1 : (byte)0
    ];
}
