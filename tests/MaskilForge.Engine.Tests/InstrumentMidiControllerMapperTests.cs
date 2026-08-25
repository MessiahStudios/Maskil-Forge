using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class InstrumentMidiControllerMapperTests
{
    [Fact]
    public void Map_AssignsInspectableDynamicsControllersFromSwellArticulations()
    {
        var set = InstrumentMidiControllerMapper.Map();

        Assert.Equal(
            [
                ("cello", true, InstrumentArticulation.BowExpression, "Expression", 11),
                ("acoustic-guitar", true, InstrumentArticulation.Picking, "Expression", 11),
                ("piano", true, InstrumentArticulation.Strike, "Expression", 11),
                ("electric-bass", true, InstrumentArticulation.Finger, "Expression", 11),
                ("drum-kit", false, null, null, null),
                ("violin", true, InstrumentArticulation.BowExpression, "Expression", 11),
                ("flute", true, InstrumentArticulation.Breath, "Breath Controller", 2),
                ("clarinet", true, InstrumentArticulation.Legato, "Expression", 11),
                ("trumpet", true, InstrumentArticulation.Legato, "Expression", 11),
                ("synth-pad", true, InstrumentArticulation.Pad, "Expression", 11),
                ("synth-lead", true, InstrumentArticulation.Filter, "Brightness", 74),
                ("electric-guitar", true, InstrumentArticulation.Distortion, "Expression", 11),
            ],
            set.Assignments.Select(item => (
                item.InstrumentId,
                item.Applicable,
                item.Articulation,
                item.ControllerName,
                item.ControllerNumber)));
        Assert.Equal("Flute", Assert.Single(set.Assignments, item => item.InstrumentId == "flute").InstrumentName);
    }
}
