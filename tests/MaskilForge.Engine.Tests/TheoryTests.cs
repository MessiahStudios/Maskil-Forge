using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class TheoryTests
{
    [Fact]
    public void PitchSpelling_MapsAccidentalsToPitchClasses()
    {
        Assert.Equal(0, new PitchSpelling(NoteLetter.C).PitchClass.Value);
        Assert.Equal(1, new PitchSpelling(NoteLetter.C, Accidental.Sharp).PitchClass.Value);
        Assert.Equal(1, new PitchSpelling(NoteLetter.D, Accidental.Flat).PitchClass.Value);
        Assert.Equal(11, new PitchSpelling(NoteLetter.B).PitchClass.Value);
    }

    [Fact]
    public void ScalePitchClasses_MajorAndNaturalMinor_AreDeterministic()
    {
        Assert.Equal(
            [0, 2, 4, 5, 7, 9, 11],
            Theory.ScalePitchClasses(new PitchClass(0), ScaleMode.Major).Select(item => item.Value));
        Assert.Equal(
            [9, 11, 0, 2, 4, 5, 7],
            Theory.ScalePitchClasses(new PitchClass(9), ScaleMode.NaturalMinor).Select(item => item.Value));
    }

    [Fact]
    public void ChordPitchClasses_CoverSmallVocabulary()
    {
        Assert.Equal([0, 4, 7], Theory.ChordPitchClasses(new PitchClass(0), ChordQuality.Major).Select(item => item.Value));
        Assert.Equal([2, 5, 9], Theory.ChordPitchClasses(new PitchClass(2), ChordQuality.Minor).Select(item => item.Value));
        Assert.Equal([0, 3, 6], Theory.ChordPitchClasses(new PitchClass(0), ChordQuality.Diminished).Select(item => item.Value));
        Assert.Equal([0, 4, 8], Theory.ChordPitchClasses(new PitchClass(0), ChordQuality.Augmented).Select(item => item.Value));
        Assert.Equal([7, 11, 2, 5], Theory.ChordPitchClasses(new PitchClass(7), ChordQuality.DominantSeventh).Select(item => item.Value));
    }

    [Fact]
    public void Transpose_PreservesModeAndChordQuality()
    {
        var key = new MusicalKey(NoteLetter.C, Accidental.Natural, ScaleMode.Major).Transpose(2);
        Assert.Equal(NoteLetter.D, key.Tonic);
        Assert.Equal(Accidental.Natural, key.Accidental);
        Assert.Equal(ScaleMode.Major, key.Mode);

        var chord = new ChordSymbol(NoteLetter.G, Accidental.Natural, ChordQuality.DominantSeventh).Transpose(-2);
        Assert.Equal([5, 9, 0, 3], chord.PitchClasses.Select(item => item.Value));
        Assert.Equal("F7", chord.ToDisplayString());
    }

    [Fact]
    public void SetKeyCommand_UndoRestoresPreviousKey()
    {
        var project = SongProject.Create("Theory");
        Assert.Equal("C major", project.Key.ToDisplayString());

        var editor = new ProjectEditor(project);
        editor.Execute(new SetKeyCommand(new MusicalKey(NoteLetter.A, Accidental.Natural, ScaleMode.NaturalMinor)));
        Assert.Equal("A natural minor", editor.Project.Key.ToDisplayString());

        editor.Undo();
        Assert.Equal("C major", editor.Project.Key.ToDisplayString());
        editor.Redo();
        Assert.Equal("A natural minor", editor.Project.Key.ToDisplayString());
    }

    [Fact]
    public void IntervalSemitones_WrapsWithinOctave()
    {
        Assert.Equal(7, Theory.IntervalSemitones(new PitchClass(0), new PitchClass(7)));
        Assert.Equal(1, Theory.IntervalSemitones(new PitchClass(11), new PitchClass(0)));
    }
}
