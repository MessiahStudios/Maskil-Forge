using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class InstrumentMidiChannelMapperTests
{
    [Fact]
    public void Map_AssignsCatalogOrderChannelsWithoutUsingDrumChannelForPitchedInstruments()
    {
        var set = InstrumentMidiChannelMapper.Map();

        Assert.Equal(1, set.UnassignedMidiChannel);
        Assert.Equal(
            [
                ("cello", 2),
                ("acoustic-guitar", 3),
                ("piano", 4),
                ("electric-bass", 5),
                ("drum-kit", 10),
                ("violin", 6),
                ("flute", 7),
                ("clarinet", 8),
                ("trumpet", 9),
                ("synth-pad", 11),
                ("synth-lead", 12),
                ("electric-guitar", 13),
            ],
            set.Assignments.Select(item => (item.InstrumentId, item.MidiChannel)));
        Assert.Equal("Cello", Assert.Single(set.Assignments, item => item.InstrumentId == "cello").InstrumentName);
        Assert.DoesNotContain(set.Assignments.Where(item => item.InstrumentId != "drum-kit"), item => item.MidiChannel == 10);
        Assert.All(set.Assignments, item => Assert.InRange(item.MidiChannel, 1, 16));
    }
}
