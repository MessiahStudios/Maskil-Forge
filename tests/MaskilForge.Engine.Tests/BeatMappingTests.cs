using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class BeatMappingTests
{
    [Fact]
    public void BeatPosition_RejectsNonMusicalCoordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BeatPosition(0, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BeatPosition(1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BeatPosition(1, 1, -1));
    }

    [Fact]
    public void ArtistPlacement_ConnectsAStableSyllableToSectionRelativeAndSongTime()
    {
        var project = SongProject.Create("Beat Map");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("through pain");
        var word = line.Words[1];
        line.SetSyllables(word.Id, ["pain"]);
        var syllableId = word.Syllables[0].Id;

        project.SetSyllablePlacement(
            section.Id,
            line.Id,
            syllableId,
            new BeatPosition(2, 3, 120));

        var placement = Assert.Single(line.SyllablePlacements);
        Assert.Equal(syllableId, placement.SyllableId);
        Assert.Equal(new BeatPosition(2, 3, 120), placement.Position);
        Assert.Equal(PlacementProvenance.Manual, placement.Provenance);
        Assert.Equal(new MusicalPosition(2, 3, 120), project.ResolveSyllablePosition(section.Id, placement.Position));
    }

    [Fact]
    public void PlacementEdit_PreservesIdentifierAndCanRecordNonManualProvenance()
    {
        var (project, section, line, syllableId) = CreateSingleSyllableProject();
        project.SetSyllablePlacement(section.Id, line.Id, syllableId, new BeatPosition(1, 1, 0));
        var placementId = line.SyllablePlacements[0].Id;

        project.SetSyllablePlacement(
            section.Id,
            line.Id,
            syllableId,
            new BeatPosition(2, 1, 0),
            PlacementProvenance.Imported);

        var placement = Assert.Single(line.SyllablePlacements);
        Assert.Equal(placementId, placement.Id);
        Assert.Equal(new BeatPosition(2, 1, 0), placement.Position);
        Assert.Equal(PlacementProvenance.Imported, placement.Provenance);
    }

    [Fact]
    public void Placement_RejectsCoordinatesOutsideSectionAndMeter()
    {
        var (project, section, line, syllableId) = CreateSingleSyllableProject();

        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetSyllablePlacement(
            section.Id, line.Id, syllableId, new BeatPosition(9, 1, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetSyllablePlacement(
            section.Id, line.Id, syllableId, new BeatPosition(1, 5, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetSyllablePlacement(
            section.Id, line.Id, syllableId, new BeatPosition(1, 1, 480)));
        Assert.Empty(line.SyllablePlacements);
    }

    [Fact]
    public void Placement_RejectsMusicalTimeThatReversesLyricOrder()
    {
        var project = SongProject.Create("Beat Map");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("one two");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var first = line.Words[0].Syllables[0].Id;
        var second = line.Words[1].Syllables[0].Id;
        project.SetSyllablePlacement(section.Id, line.Id, second, new BeatPosition(1, 2, 0));

        Assert.Throws<ArgumentException>(() => project.SetSyllablePlacement(
            section.Id, line.Id, first, new BeatPosition(1, 3, 0)));
        Assert.Equal(second, Assert.Single(line.SyllablePlacements).SyllableId);
    }

    [Fact]
    public void CompatibleLyricAndBoundaryEdits_PreservePlacementIdentity()
    {
        var project = SongProject.Create("Beat Map");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("Amazing grace");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["A", "maz", "ing"]);
        var syllableId = word.Syllables[1].Id;
        project.SetSyllablePlacement(section.Id, line.Id, syllableId, new BeatPosition(1, 2, 0));
        var placementId = line.SyllablePlacements[0].Id;

        line.SetSyllables(word.Id, ["uh", "A", "maz", "ing"]);
        line.SetText("Oh Amazing grace");

        var placement = Assert.Single(line.SyllablePlacements);
        Assert.Equal(placementId, placement.Id);
        Assert.Equal(syllableId, placement.SyllableId);
    }

    [Fact]
    public void RemovingTheReferencedSyllable_RemovesItsPlacement()
    {
        var (project, section, line, syllableId) = CreateSingleSyllableProject();
        var word = line.Words[0];
        project.SetSyllablePlacement(section.Id, line.Id, syllableId, new BeatPosition(1, 1, 0));

        line.SetSyllables(word.Id, ["changed"]);

        Assert.Empty(line.SyllablePlacements);
    }

    [Fact]
    public void ReorderingASection_MovesAbsoluteTimeButPreservesItsRelativeAnchor()
    {
        var project = SongProject.Create("Beat Map");
        var verse = project.AddSection(SectionKind.Verse);
        var line = verse.AddLyricLine("home");
        line.SetSyllables(line.Words[0].Id, ["home"]);
        var syllableId = line.Words[0].Syllables[0].Id;
        project.SetSyllablePlacement(verse.Id, line.Id, syllableId, new BeatPosition(2, 1, 0));
        var placementId = line.SyllablePlacements[0].Id;
        var chorus = project.AddSection(SectionKind.Chorus);

        project.MoveSection(verse.Id, 1);

        var placement = Assert.Single(line.SyllablePlacements);
        Assert.Equal(placementId, placement.Id);
        Assert.Equal(new BeatPosition(2, 1, 0), placement.Position);
        Assert.Equal(new MusicalPosition(10, 1, 0), project.ResolveSyllablePosition(verse.Id, placement.Position));
        Assert.Equal(chorus.Id, project.Sections[0].Id);
    }

    [Fact]
    public void RemovingAndUndoingASection_RestoresItsExactSyllableAnchor()
    {
        var (project, section, line, syllableId) = CreateSingleSyllableProject();
        project.SetSyllablePlacement(section.Id, line.Id, syllableId, new BeatPosition(2, 1, 0));
        var placementId = line.SyllablePlacements[0].Id;
        var editor = new ProjectEditor(project);

        editor.Execute(new RemoveSectionCommand(section.Id));
        Assert.Empty(project.Sections);

        Assert.True(editor.Undo());
        var restoredLine = project.Sections[0].LyricLines[0];
        var restored = Assert.Single(restoredLine.SyllablePlacements);
        Assert.Equal(placementId, restored.Id);
        Assert.Equal(syllableId, restored.SyllableId);
        Assert.Equal(new BeatPosition(2, 1, 0), restored.Position);
    }

    [Fact]
    public void TimelineChanges_CannotInvalidateExistingArtistPlacements()
    {
        var (project, section, line, syllableId) = CreateSingleSyllableProject();
        project.SetSyllablePlacement(section.Id, line.Id, syllableId, new BeatPosition(8, 4, 0));

        Assert.Throws<InvalidOperationException>(() => project.SetSectionDuration(section.Id, 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetTimeSignature(3, 4));
        Assert.Equal(8, project.Timeline.FindSection(section.Id).DurationBars);
        Assert.Equal((4, 4), (project.TimeSignature.Numerator, project.TimeSignature.Denominator));
    }

    [Fact]
    public void LyricLine_RejectsSerializedPlacementForAnUnknownSyllable()
    {
        var source = LyricLine.Create("home");
        source.SetSyllables(source.Words[0].Id, ["home"]);
        var placement = new SyllablePlacement(
            SyllablePlacementId.New(),
            SyllableId.New(),
            new BeatPosition(1, 1, 0),
            PlacementProvenance.Imported);

        Assert.Throws<ArgumentException>(() => new LyricLine(
            LyricLineId.New(),
            source.Text,
            source.Words,
            source.Punctuation,
            source.Phrases,
            [placement]));
    }

    private static (SongProject Project, SongSection Section, LyricLine Line, SyllableId SyllableId)
        CreateSingleSyllableProject()
    {
        var project = SongProject.Create("Beat Map");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("home");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["home"]);
        return (project, section, line, word.Syllables[0].Id);
    }
}
