using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class InstrumentMidiPitchBendMapperTests
{
    [Fact]
    public void Map_AssignsInspectablePitchBendRangeFromSlideArticulations()
    {
        var set = InstrumentMidiPitchBendMapper.Map();

        Assert.Equal(
            [
                ("cello", true, InstrumentArticulation.Slide, 2),
                ("acoustic-guitar", true, InstrumentArticulation.Bend, 2),
                ("piano", false, null, null),
                ("electric-bass", false, null, null),
                ("drum-kit", false, null, null),
                ("violin", true, InstrumentArticulation.Slide, 2),
                ("flute", false, null, null),
                ("clarinet", false, null, null),
                ("trumpet", false, null, null),
                ("synth-pad", false, null, null),
                ("synth-lead", false, InstrumentArticulation.Portamento, null),
                ("electric-guitar", true, InstrumentArticulation.Bend, 2),
            ],
            set.Assignments.Select(item => (
                item.InstrumentId,
                item.Applicable,
                item.Articulation,
                item.RangeSemitones)));
        Assert.Equal("Cello", Assert.Single(set.Assignments, item => item.InstrumentId == "cello").InstrumentName);
        Assert.Equal("Synth Lead", Assert.Single(set.Assignments, item => item.InstrumentId == "synth-lead").InstrumentName);
    }
}
