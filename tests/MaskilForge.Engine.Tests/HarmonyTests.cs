using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class HarmonyTests
{
    [Fact]
    public void AddHarmonyChord_StoresOrderedSectionProgression()
    {
        var project = SongProject.Create("Harmony");
        var section = project.AddSection(SectionKind.Verse);
        project.SetSectionDuration(section.Id, 8);

        var second = project.AddHarmonyChord(
            section.Id,
            new ChordSymbol(NoteLetter.G, Accidental.Natural, ChordQuality.Major),
            new BeatPosition(3, 1, 0),
            2);
        var first = project.AddHarmonyChord(
            section.Id,
            new ChordSymbol(NoteLetter.C, Accidental.Natural, ChordQuality.Major),
            new BeatPosition(1, 1, 0),
            2);

        Assert.Equal([first.Id, second.Id], section.Harmony.Select(item => item.Id));
        Assert.Equal("C", first.Chord.ToDisplayString());
        Assert.Equal(HarmonyProvenance.Manual, first.Provenance);
    }

    [Fact]
    public void HarmonyCommands_UndoAndRedoPreserveIdentities()
    {
        var project = SongProject.Create("Harmony");
        var section = project.AddSection(SectionKind.Chorus);
        var editor = new ProjectEditor(project);

        editor.Execute(new AddHarmonyChordCommand(
            section.Id,
            new ChordSymbol(NoteLetter.A, Accidental.Natural, ChordQuality.Minor),
            new BeatPosition(1, 1, 0),
            4));
        var id = Assert.Single(section.Harmony).Id;

        editor.Execute(new SetHarmonyChordCommand(
            section.Id,
            id,
            new ChordSymbol(NoteLetter.E, Accidental.Natural, ChordQuality.Minor),
            new BeatPosition(2, 1, 0),
            2));
        Assert.Equal("Em", section.Harmony[0].Chord.ToDisplayString());
        Assert.Equal(2, section.Harmony[0].Start.Bar);

        editor.Undo();
        Assert.Equal("Am", section.Harmony[0].Chord.ToDisplayString());
        Assert.Equal(id, section.Harmony[0].Id);

        editor.Undo();
        Assert.Empty(section.Harmony);
        editor.Redo();
        Assert.Equal(id, Assert.Single(section.Harmony).Id);
    }

    [Fact]
    public void RomanNumeral_ReportsDiatonicLabels()
    {
        var key = new MusicalKey(NoteLetter.C, Accidental.Natural, ScaleMode.Major);
        Assert.Equal("I", Theory.RomanNumeral(key, new ChordSymbol(NoteLetter.C)));
        Assert.Equal("V7", Theory.RomanNumeral(key, new ChordSymbol(NoteLetter.G, Accidental.Natural, ChordQuality.DominantSeventh)));
        Assert.Equal("vi", Theory.RomanNumeral(key, new ChordSymbol(NoteLetter.A, Accidental.Natural, ChordQuality.Minor)));
        Assert.Null(Theory.RomanNumeral(key, new ChordSymbol(NoteLetter.C, Accidental.Natural, ChordQuality.Minor)));
    }

    [Fact]
    public void SectionDuration_CannotInvalidateHarmonySpan()
    {
        var project = SongProject.Create("Harmony");
        var section = project.AddSection(SectionKind.Verse);
        project.AddHarmonyChord(
            section.Id,
            new ChordSymbol(NoteLetter.F),
            new BeatPosition(5, 1, 0),
            2);

        Assert.Throws<InvalidOperationException>(() => project.SetSectionDuration(section.Id, 4));
    }
}
