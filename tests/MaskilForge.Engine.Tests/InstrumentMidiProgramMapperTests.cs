using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class InstrumentMidiProgramMapperTests
{
    [Fact]
    public void Map_AssignsInspectableGeneralMidiProgramsWithoutADrumKitProgram()
    {
        var set = InstrumentMidiProgramMapper.Map();

        Assert.Equal(
            [
                ("cello", true, "Cello", 43),
                ("acoustic-guitar", true, "Acoustic Guitar (steel)", 26),
                ("piano", true, "Acoustic Grand Piano", 1),
                ("electric-bass", true, "Electric Bass (finger)", 34),
                ("drum-kit", false, null, null),
                ("violin", true, "Violin", 41),
                ("flute", true, "Flute", 74),
                ("clarinet", true, "Clarinet", 72),
                ("trumpet", true, "Trumpet", 57),
                ("synth-pad", true, "Pad 2 (warm)", 90),
                ("synth-lead", true, "Lead 2 (sawtooth)", 82),
                ("electric-guitar", true, "Distortion Guitar", 31),
            ],
            set.Assignments.Select(item => (item.InstrumentId, item.Applicable, item.ProgramName, item.MidiProgram)));
        Assert.Equal("Cello", Assert.Single(set.Assignments, item => item.InstrumentId == "cello").InstrumentName);
        Assert.Equal(42, InstrumentMidiProgramMapper.ZeroBasedProgram(43));
        Assert.All(set.Assignments.Where(item => item.Applicable), item => Assert.InRange(item.MidiProgram!.Value, 1, 128));
    }
}
