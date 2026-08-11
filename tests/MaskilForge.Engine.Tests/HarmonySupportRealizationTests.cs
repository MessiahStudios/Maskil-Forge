using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class HarmonySupportRealizationTests
{
    [Fact]
    public void Proposal_UsesRegisteredVoicingsFromApprovedHarmony()
    {
        var project = SongProject.Create("Harmony support");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.Harmony);
        var chord = project.AddHarmonyChord(
            section.Id,
            new ChordSymbol(NoteLetter.C, Accidental.Natural, ChordQuality.Major),
            new BeatPosition(1, 1, 0));
        project.SetChordVoicing(section.Id, chord.Id, [
            new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3),
            new RegisteredPitch(NoteLetter.E, Accidental.Natural, 3),
            new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3)]);

        var proposal = HarmonySupportRealizer.Propose(project, section.Id);

        Assert.Equal("Chorus harmony support", proposal.PartLabel);
        Assert.False(proposal.UsesPreviewVoicings);
        Assert.Equal(0, proposal.ReusedNoteCount);
        Assert.Equal([48, 52, 55], proposal.Events.Select(item => item.Pitch.MidiNumber));
        Assert.All(proposal.Events, item =>
        {
            Assert.Equal(0, item.StartTick);
            Assert.Equal(1_920, item.DurationTicks);
            Assert.False(item.UsesPreviewVoicing);
            Assert.Null(item.ExistingNoteEventId);
        });
    }

    [Fact]
    public void Acceptance_CreatesOnePartAndIsExactlyReversible()
    {
        var editor = new ProjectEditor(SongProject.Create("Accepted harmony support"));
        var section = editor.Project.AddSection(SectionKind.Verse);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Harmony);
        editor.Project.AddHarmonyChord(
            section.Id,
            new ChordSymbol(NoteLetter.G, Accidental.Natural, ChordQuality.Major),
            new BeatPosition(1, 1, 0));
        var originalIds = editor.Project.NoteEvents.Select(item => item.Id).ToList();

        editor.Execute(new UseHarmonySupportProposalCommand(section.Id));
        var createdPart = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal(ArrangementRole.Harmony, createdPart.Role);
        Assert.Equal(3, createdPart.NoteEventIds.Count);
        Assert.Equal(3, editor.Project.NoteEvents.Count);
        Assert.All(createdPart.NoteEventIds, id => Assert.DoesNotContain(id, originalIds));

        editor.Undo();
        Assert.Empty(editor.Project.MusicalParts);
        Assert.Equal(originalIds, editor.Project.NoteEvents.Select(item => item.Id));
        editor.Redo();
        Assert.Equal(createdPart.Id, Assert.Single(editor.Project.MusicalParts).Id);
        Assert.Equal(createdPart.NoteEventIds, Assert.Single(editor.Project.MusicalParts).NoteEventIds);
    }

    [Fact]
    public void Acceptance_ReusesExistingSketchNotesWithoutDuplicatingThem()
    {
        var editor = new ProjectEditor(SongProject.Create("Reuse sketch notes"));
        var section = editor.Project.AddSection(SectionKind.Bridge);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Harmony);
        editor.Project.AddHarmonyChord(
            section.Id,
            new ChordSymbol(NoteLetter.C, Accidental.Natural, ChordQuality.Major),
            new BeatPosition(1, 1, 0));
        editor.Execute(new UseHarmonyNoteSketchCommand(section.Id));
        var sketchIds = editor.Project.NoteEvents.Select(item => item.Id).OrderBy(item => item.Value).ToList();

        var proposal = HarmonySupportRealizer.Propose(editor.Project, section.Id);
        Assert.Equal(3, proposal.ReusedNoteCount);
        Assert.All(proposal.Events, item => Assert.NotNull(item.ExistingNoteEventId));

        editor.Execute(new UseHarmonySupportProposalCommand(section.Id));
        Assert.Equal(sketchIds, editor.Project.NoteEvents.Select(item => item.Id).OrderBy(item => item.Value));
        Assert.Equal(
            sketchIds,
            Assert.Single(editor.Project.MusicalParts).NoteEventIds.OrderBy(item => item.Value));
        editor.Undo();
        Assert.Empty(editor.Project.MusicalParts);
        Assert.Equal(sketchIds, editor.Project.NoteEvents.Select(item => item.Id).OrderBy(item => item.Value));
    }

    [Fact]
    public void Proposal_RequiresRoleAndHarmony()
    {
        var project = SongProject.Create("Requirements");
        var section = project.AddSection(SectionKind.Verse);
        Assert.Throws<InvalidOperationException>(() => HarmonySupportRealizer.Propose(project, section.Id));
        project.SetSectionRole(section.Id, ArrangementRole.Harmony);
        Assert.Throws<InvalidOperationException>(() => HarmonySupportRealizer.Propose(project, section.Id));
    }
}
