using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// One inspectable General MIDI percussion pitch for drum-kit Hit. It is not a
/// chosen kick, snare, or cymbal, a MIDI channel, or a program change.
/// </summary>
public sealed record DrumKitGeneralMidiPiece(
    string Id,
    string Name,
    RegisteredPitch Pitch);

public sealed record DrumKitGeneralMidiMap(
    string InstrumentId,
    string InstrumentName,
    DrumKitGeneralMidiPiece Hit);

public static class DrumKitGeneralMidiMapper
{
    public const string DrumKitInstrumentId = "drum-kit";
    public const string AcousticBassDrumId = "acoustic-bass-drum";
    public const string AcousticBassDrumName = "Acoustic Bass Drum";
    public static readonly RegisteredPitch AcousticBassDrumPitch =
        new(NoteLetter.C, Accidental.Natural, 2);

    public static DrumKitGeneralMidiMap Map() => new(
        DrumKitInstrumentId,
        "Drum Kit",
        new DrumKitGeneralMidiPiece(AcousticBassDrumId, AcousticBassDrumName, AcousticBassDrumPitch));
}
