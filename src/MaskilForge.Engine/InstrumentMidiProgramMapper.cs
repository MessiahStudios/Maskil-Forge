using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// An inspectable General MIDI program for one catalog instrument. Program
/// numbers are musician-facing 1-128. Drum kit has no program: channel 10 is
/// the percussion identity. This map does not choose a renderer or a VST.
/// </summary>
public sealed record InstrumentMidiProgramAssignment(
    string InstrumentId,
    string InstrumentName,
    bool Applicable,
    string? ProgramName,
    int? MidiProgram);

public sealed record InstrumentMidiProgramMapSet(
    IReadOnlyList<InstrumentMidiProgramAssignment> Assignments);

public static class InstrumentMidiProgramMapper
{
    private static readonly Dictionary<string, (string ProgramName, int MidiProgram)> Known = new(StringComparer.Ordinal)
    {
        ["cello"] = ("Cello", 43),
        ["acoustic-guitar"] = ("Acoustic Guitar (steel)", 26),
        ["piano"] = ("Acoustic Grand Piano", 1),
        ["electric-bass"] = ("Electric Bass (finger)", 34),
        ["violin"] = ("Violin", 41),
        ["flute"] = ("Flute", 74),
        ["clarinet"] = ("Clarinet", 72),
        ["trumpet"] = ("Trumpet", 57),
        ["synth-pad"] = ("Pad 2 (warm)", 90),
        ["synth-lead"] = ("Lead 2 (sawtooth)", 82),
        ["electric-guitar"] = ("Distortion Guitar", 31),
    };

    public static InstrumentMidiProgramMapSet Map(InstrumentProfileCatalog? catalog = null)
    {
        catalog ??= InstrumentProfileCatalogLoader.Current;
        var assignments = catalog.Instruments.Select(MapInstrument).ToList();
        return new InstrumentMidiProgramMapSet(assignments);
    }

    public static byte ZeroBasedProgram(int midiProgram) => checked((byte)(midiProgram - 1));

    private static InstrumentMidiProgramAssignment MapInstrument(InstrumentProfile instrument)
    {
        if (string.Equals(instrument.Id, DrumKitGeneralMidiMapper.DrumKitInstrumentId, StringComparison.Ordinal))
            return new InstrumentMidiProgramAssignment(instrument.Id, instrument.Name, false, null, null);

        if (!Known.TryGetValue(instrument.Id, out var known))
        {
            throw new InvalidOperationException(
                $"Catalog instrument '{instrument.Id}' does not have an inspectable General MIDI program.");
        }

        return new InstrumentMidiProgramAssignment(
            instrument.Id,
            instrument.Name,
            true,
            known.ProgramName,
            known.MidiProgram);
    }
}
