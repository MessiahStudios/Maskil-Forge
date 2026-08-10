using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class HarmonyNoteSketchTests
{
    [Fact]
    public void Project_UsesRegisteredVoicesAndAbsoluteSectionTiming()
    {
        var project = SongProject.Create("Registered sketch");
        var verse = project.AddSection(SectionKind.Verse);
        project.SetSectionDuration(verse.Id, 4);
        var chorus = project.AddSection(SectionKind.Chorus);
        var chord = project.AddHarmonyChord(
            chorus.Id,
            new ChordSymbol(NoteLetter.G, Accidental.Natural, ChordQuality.Major),
            new BeatPosition(2, 2, 240));
        project.SetChordVoicing(chorus.Id, chord.Id, [
            new RegisteredPitch(NoteLetter.B, Accidental.Natural, 2),
            new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3),
            new RegisteredPitch(NoteLetter.D, Accidental.Natural, 4)]);

        var sketch = HarmonyNoteSketcher.Project(project, chorus.Id);

        Assert.False(sketch.UsesPreviewVoicings);
        Assert.Equal([47, 55, 62], sketch.Events.Select(item => item.Pitch.MidiNumber));
        Assert.All(sketch.Events, item =>
        {
            Assert.Equal(10_320, item.StartTick);
            Assert.Equal(1_920, item.DurationTicks);
            Assert.Equal(96, item.Velocity);
            Assert.False(item.UsesPreviewVoicing);
        });
    }

    [Fact]
    public void Project_UsesClearlyMarkedTemporaryPreviewVoicingWhenNeeded()
    {
        var project = SongProject.Create("Preview sketch");
        var section = project.AddSection(SectionKind.Verse);
        project.AddHarmonyChord(
            section.Id,
            new ChordSymbol(NoteLetter.F, Accidental.Sharp, ChordQuality.Minor),
            new BeatPosition(1, 1, 0));

        var sketch = HarmonyNoteSketcher.Project(project, section.Id);

        Assert.True(sketch.UsesPreviewVoicings);
        Assert.Equal([54, 57, 61], sketch.Events.Select(item => item.Pitch.MidiNumber));
        Assert.All(sketch.Events, item => Assert.True(item.UsesPreviewVoicing));
    }

    [Fact]
    public void UseHarmonyNoteSketchCommand_IsExplicitAdditiveAndReversible()
    {
        var project = SongProject.Create("Accepted sketch");
        var section = project.AddSection(SectionKind.Verse);
        project.AddHarmonyChord(
            section.Id,
            new ChordSymbol(NoteLetter.C, Accidental.Natural, ChordQuality.Major),
            new BeatPosition(1, 1, 0));
        var manual = project.AddNoteEvent(new RegisteredPitch(NoteLetter.A, Accidental.Natural, 4), 240, 120, 70);
        var editor = new ProjectEditor(project);
        var command = new UseHarmonyNoteSketchCommand(section.Id);

        editor.Execute(command);
        var acceptedIds = editor.Project.NoteEvents.Where(item => item.Id != manual.Id).Select(item => item.Id).ToList();
        Assert.Equal(3, acceptedIds.Count);
        Assert.Contains(editor.Project.NoteEvents, item => item.Id == manual.Id);

        editor.Undo();
        Assert.Equal(manual.Id, Assert.Single(editor.Project.NoteEvents).Id);

        editor.Redo();
        Assert.Equal(acceptedIds, editor.Project.NoteEvents.Where(item => item.Id != manual.Id).Select(item => item.Id));
    }

    [Fact]
    public void Project_RequiresExistingHarmony()
    {
        var project = SongProject.Create("No harmony");
        var section = project.AddSection(SectionKind.Verse);

        var error = Assert.Throws<InvalidOperationException>(() => HarmonyNoteSketcher.Project(project, section.Id));

        Assert.Contains("Add at least one harmony chord", error.Message);
    }
}
