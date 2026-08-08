using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class TimelineTests
{
    [Fact]
    public void Resolution_IsFixedAt480Ppq()
    {
        var project = SongProject.Create("Clock");

        Assert.Equal(480, TimelineResolution.TicksPerQuarterNote);
        Assert.Equal(480, project.Timeline.TicksPerQuarterNote);
    }

    [Fact]
    public void MusicalPosition_RoundTripsThroughAbsoluteTicksInFourFour()
    {
        var timeline = SongTimeline.CreateDefault();
        var position = new MusicalPosition(4, 2, 120);

        var ticks = timeline.ToAbsoluteTicks(position);

        Assert.Equal(6_360, ticks);
        Assert.Equal(position, timeline.FromAbsoluteTicks(ticks));
    }

    [Fact]
    public void MusicalPosition_UsesMeterDenominatorForTicksPerBeat()
    {
        var timeline = SongTimeline.CreateDefault();
        timeline.TimeSignatureMap.SetInitialTimeSignature(6, 8);

        Assert.Equal(1_680, timeline.ToAbsoluteTicks(new MusicalPosition(2, 2, 0)));
        Assert.Equal(new MusicalPosition(2, 2, 0), timeline.FromAbsoluteTicks(1_680));
    }

    [Fact]
    public void MusicalPosition_RejectsCoordinatesOutsideTheMeter()
    {
        var timeline = SongTimeline.CreateDefault();

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.ToAbsoluteTicks(new MusicalPosition(1, 5, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.ToAbsoluteTicks(new MusicalPosition(1, 1, 480)));
    }

    [Fact]
    public void SectionOperations_ReflowPlacementsAndPreserveDurations()
    {
        var project = SongProject.Create("Arrangement");
        var verse = project.AddSection(SectionKind.Verse);
        var chorus = project.AddSection(SectionKind.Chorus);

        AssertPlacement(project, verse.Id, 1, 8);
        AssertPlacement(project, chorus.Id, 9, 8);

        project.SetSectionDuration(verse.Id, 4);
        AssertPlacement(project, verse.Id, 1, 4);
        AssertPlacement(project, chorus.Id, 5, 8);

        project.MoveSection(chorus.Id, 0);
        AssertPlacement(project, chorus.Id, 1, 8);
        AssertPlacement(project, verse.Id, 9, 4);
    }

    [Fact]
    public void SetSectionDuration_UndoAndRedoRestoreTheTimeline()
    {
        var project = SongProject.Create("History");
        var verse = project.AddSection(SectionKind.Verse);
        var chorus = project.AddSection(SectionKind.Chorus);
        var editor = new ProjectEditor(project);

        editor.Execute(new SetSectionDurationCommand(verse.Id, 12));
        AssertPlacement(project, verse.Id, 1, 12);
        AssertPlacement(project, chorus.Id, 13, 8);

        Assert.True(editor.Undo());
        AssertPlacement(project, verse.Id, 1, 8);
        AssertPlacement(project, chorus.Id, 9, 8);

        Assert.True(editor.Redo());
        AssertPlacement(project, verse.Id, 1, 12);
        AssertPlacement(project, chorus.Id, 13, 8);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(129)]
    public void SetSectionDuration_RejectsInvalidLengths(int durationBars)
    {
        var project = SongProject.Create("Validation");
        var section = project.AddSection(SectionKind.Verse);

        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetSectionDuration(section.Id, durationBars));
    }

    [Fact]
    public void SectionPlacement_RejectsInvalidPersistedLength()
    {
        var sectionId = SectionId.New();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SectionPlacement(sectionId, new MusicalPosition(1, 1, 0), 129));
    }

    private static void AssertPlacement(SongProject project, SectionId sectionId, int startBar, int durationBars)
    {
        var placement = project.Timeline.FindSection(sectionId);
        Assert.Equal(startBar, placement.Start.Bar);
        Assert.Equal(1, placement.Start.Beat);
        Assert.Equal(0, placement.Start.Tick);
        Assert.Equal(durationBars, placement.DurationBars);
    }
}
