using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class InstrumentMidiPortamentoMapperTests
{
    [Fact]
    public void Map_AssignsInspectablePortamentoControllerFromSlideArticulation()
    {
        var set = InstrumentMidiPortamentoMapper.Map();

        Assert.Equal(
            [
                ("cello", false, null, null, null),
                ("acoustic-guitar", false, null, null, null),
                ("piano", false, null, null, null),
                ("electric-bass", false, null, null, null),
                ("drum-kit", false, null, null, null),
                ("violin", false, null, null, null),
                ("flute", false, null, null, null),
                ("clarinet", false, null, null, null),
                ("trumpet", false, null, null, null),
                ("synth-pad", false, null, null, null),
                ("synth-lead", true, InstrumentArticulation.Portamento, "Portamento", 65),
                ("electric-guitar", false, null, null, null),
            ],
            set.Assignments.Select(item => (
                item.InstrumentId,
                item.Applicable,
                item.Articulation,
                item.ControllerName,
                item.ControllerNumber)));
        Assert.Equal("Synth Lead", Assert.Single(set.Assignments, item => item.InstrumentId == "synth-lead").InstrumentName);
    }
}
