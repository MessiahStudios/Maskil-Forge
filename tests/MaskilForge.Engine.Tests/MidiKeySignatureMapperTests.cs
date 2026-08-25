using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class MidiKeySignatureMapperTests
{
    [Fact]
    public void Map_AssignsInspectableSignaturesForConventionalMajorAndMinorKeys()
    {
        Assert.Equal(new MidiKeySignature(0, false), MidiKeySignatureMapper.Map(MusicalKey.Default));
        Assert.Equal(new MidiKeySignature(1, false), MidiKeySignatureMapper.Map(new MusicalKey(NoteLetter.G, Accidental.Natural, ScaleMode.Major)));
        Assert.Equal(new MidiKeySignature(-1, false), MidiKeySignatureMapper.Map(new MusicalKey(NoteLetter.F, Accidental.Natural, ScaleMode.Major)));
        Assert.Equal(new MidiKeySignature(0, true), MidiKeySignatureMapper.Map(new MusicalKey(NoteLetter.A, Accidental.Natural, ScaleMode.NaturalMinor)));
        Assert.Equal(new MidiKeySignature(6, false), MidiKeySignatureMapper.Map(new MusicalKey(NoteLetter.F, Accidental.Sharp, ScaleMode.Major)));
        Assert.Equal(new MidiKeySignature(-2, false), MidiKeySignatureMapper.Map(new MusicalKey(NoteLetter.B, Accidental.Flat, ScaleMode.Major)));
    }

    [Fact]
    public void Map_OmitsSpellingsOutsideTheCircleOfFifths()
    {
        Assert.Null(MidiKeySignatureMapper.Map(new MusicalKey(NoteLetter.B, Accidental.Sharp, ScaleMode.Major)));
        Assert.Null(MidiKeySignatureMapper.Map(new MusicalKey(NoteLetter.C, Accidental.Flat, ScaleMode.NaturalMinor)));
    }
}
