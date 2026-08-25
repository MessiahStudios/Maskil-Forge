using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class DrumKitGeneralMidiMapperTests
{
    [Fact]
    public void Map_NamesAcousticBassDrumWithoutChoosingSnareOrHat()
    {
        var map = DrumKitGeneralMidiMapper.Map();

        Assert.Equal("drum-kit", map.InstrumentId);
        Assert.Equal("Drum Kit", map.InstrumentName);
        Assert.Equal("acoustic-bass-drum", map.Hit.Id);
        Assert.Equal("Acoustic Bass Drum", map.Hit.Name);
        Assert.Equal(NoteLetter.C, map.Hit.Pitch.Letter);
        Assert.Equal(Accidental.Natural, map.Hit.Pitch.Accidental);
        Assert.Equal(2, map.Hit.Pitch.Octave);
        Assert.Equal(36, map.Hit.Pitch.MidiNumber);
        Assert.NotEqual(60, map.Hit.Pitch.MidiNumber);
    }
}
