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

    [Fact]
    public void CaptureHarmonyCandidate_PreservesAnIndependentNamedProgression()
    {
        var (project, section) = CreateCandidateProject();

        var candidate = project.CaptureHarmonyCandidate(section.Id, "Verse lift");
        project.SetHarmonyChord(
            section.Id,
            section.Harmony[0].Id,
            new ChordSymbol(NoteLetter.D, Accidental.Natural, ChordQuality.Minor),
            new BeatPosition(1, 1, 0),
            2);

        Assert.Equal("Verse lift", candidate.Label);
        Assert.Equal(["C", "G7"], candidate.Events.Select(item => item.Chord.ToDisplayString()));
        Assert.Equal([0, 1], candidate.Events.Select(item => item.Position));
        Assert.Equal(2, candidate.Events.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void HarmonyCandidateCommands_UndoRedoRestoreExactCandidateAndChordIdentities()
    {
        var (project, section) = CreateCandidateProject();
        var editor = new ProjectEditor(project);
        var capture = new CaptureHarmonyCandidateCommand(section.Id, "Option A");
        editor.Execute(capture);
        var candidate = Assert.Single(section.HarmonyCandidates);
        var eventIds = candidate.Events.Select(item => item.Id).ToList();

        Assert.True(editor.Undo());
        Assert.Empty(section.HarmonyCandidates);
        Assert.True(editor.Redo());
        Assert.Equal(eventIds, Assert.Single(section.HarmonyCandidates).Events.Select(item => item.Id));

        var originalChordIds = section.Harmony.Select(item => item.Id).ToList();
        project.SetHarmonyChord(section.Id, section.Harmony[0].Id, new ChordSymbol(NoteLetter.F), new BeatPosition(1, 1, 0), 2);
        editor.Execute(new ApplyHarmonyCandidateCommand(section.Id, candidate.Id));
        Assert.Equal(["C", "G7"], section.Harmony.Select(item => item.Chord.ToDisplayString()));
        Assert.Equal(originalChordIds, section.Harmony.Select(item => item.Id));

        Assert.True(editor.Undo());
        Assert.Equal("F", section.Harmony[0].Chord.ToDisplayString());
        Assert.Equal(originalChordIds, section.Harmony.Select(item => item.Id));
    }

    [Fact]
    public void HarmonyCandidateRenameAndRemove_AreReversible()
    {
        var (project, section) = CreateCandidateProject();
        var candidate = project.CaptureHarmonyCandidate(section.Id, "Option A");
        var editor = new ProjectEditor(project);

        editor.Execute(new RenameHarmonyCandidateCommand(section.Id, candidate.Id, "Chorus lift"));
        Assert.Equal("Chorus lift", section.HarmonyCandidates[0].Label);
        Assert.True(editor.Undo());
        Assert.Equal("Option A", section.HarmonyCandidates[0].Label);

        editor.Execute(new RemoveHarmonyCandidateCommand(section.Id, candidate.Id));
        Assert.Empty(section.HarmonyCandidates);
        Assert.True(editor.Undo());
        Assert.Equal(candidate.Id, Assert.Single(section.HarmonyCandidates).Id);
    }

    [Fact]
    public void EmptyHarmony_CannotBeCapturedAsCandidate()
    {
        var project = SongProject.Create("Harmony");
        var section = project.AddSection(SectionKind.Verse);
        Assert.Throws<InvalidOperationException>(() => project.CaptureHarmonyCandidate(section.Id, "Empty"));
    }

    [Fact]
    public void SectionDuration_CannotInvalidateSavedHarmonyCandidate()
    {
        var project = SongProject.Create("Harmony candidates");
        var section = project.AddSection(SectionKind.Verse);
        project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(5, 1, 0), 2);
        project.CaptureHarmonyCandidate(section.Id, "Late cadence");
        project.RemoveHarmonyChord(section.Id, section.Harmony[0].Id);

        var exception = Assert.Throws<InvalidOperationException>(() => project.SetSectionDuration(section.Id, 4));
        Assert.Contains("harmony option", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MeterChange_CannotInvalidateSavedHarmonyCandidateBeat()
    {
        var project = SongProject.Create("Harmony candidates");
        var section = project.AddSection(SectionKind.Verse);
        project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 4, 0), 1);
        project.CaptureHarmonyCandidate(section.Id, "Four-beat push");
        project.RemoveHarmonyChord(section.Id, section.Harmony[0].Id);

        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetTimeSignature(3, 4));
        Assert.Equal(4, project.TimeSignature.Numerator);
    }

    private static (SongProject Project, SongSection Section) CreateCandidateProject()
    {
        var project = SongProject.Create("Harmony candidates");
        var section = project.AddSection(SectionKind.Verse);
        project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0), 2);
        project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.G, Accidental.Natural, ChordQuality.DominantSeventh), new BeatPosition(3, 1, 0), 2);
        return (project, section);
    }
}
