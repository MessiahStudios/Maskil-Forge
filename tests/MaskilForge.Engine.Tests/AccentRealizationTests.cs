using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class AccentRealizationTests
{
    [Fact]
    public void Proposal_MarksDownbeatsWithShortStrongHits()
    {
        var project = SongProject.Create("Accents");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionDuration(section.Id, 2);
        project.SetSectionRole(section.Id, ArrangementRole.Accent);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 80);
        var g4 = project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 4), 0, 480, 90);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 480, 240, 85);
        var c5 = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 5), 1_920, 960, 88);

        var proposal = AccentRealizer.Propose(project, section.Id);

        Assert.Equal("Chorus accents", proposal.PartLabel);
        Assert.Equal(0, proposal.ReusedNoteCount);
        Assert.Collection(proposal.Events,
            first =>
            {
                Assert.Equal("G4", first.Pitch.ToDisplayString());
                Assert.Equal(g4.Id, first.SourceNoteEventId);
                Assert.Equal(0, first.StartTick);
                Assert.Equal(120, first.DurationTicks);
                Assert.Equal(112, first.Velocity);
                Assert.Null(first.ExistingNoteEventId);
            },
            second =>
            {
                Assert.Equal("C5", second.Pitch.ToDisplayString());
                Assert.Equal(c5.Id, second.SourceNoteEventId);
                Assert.Equal(1_920, second.StartTick);
                Assert.Equal(120, second.DurationTicks);
                Assert.Equal(112, second.Velocity);
            });
    }

    [Fact]
    public void Acceptance_CreatesOnePartAndIsExactlyReversible()
    {
        var editor = new ProjectEditor(SongProject.Create("Accepted accents"));
        var section = editor.Project.AddSection(SectionKind.Verse);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Accent);
        editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 5), 0, 480, 90);
        var originalIds = editor.Project.NoteEvents.Select(item => item.Id).ToList();

        editor.Execute(new UseAccentProposalCommand(section.Id));
        var createdPart = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal(ArrangementRole.Accent, createdPart.Role);
        Assert.Single(createdPart.NoteEventIds);
        Assert.Equal(2, editor.Project.NoteEvents.Count);
        Assert.All(createdPart.NoteEventIds, id => Assert.DoesNotContain(id, originalIds));

        editor.Undo();
        Assert.Empty(editor.Project.MusicalParts);
        Assert.Equal(originalIds, editor.Project.NoteEvents.Select(item => item.Id));
        editor.Redo();
        Assert.Equal(createdPart.Id, Assert.Single(editor.Project.MusicalParts).Id);
        Assert.Equal(createdPart.NoteEventIds, Assert.Single(editor.Project.MusicalParts).NoteEventIds);
    }

    [Fact]
    public void Acceptance_ReusesAnExistingAccentNoteWithoutDuplicatingIt()
    {
        var editor = new ProjectEditor(SongProject.Create("Existing accent note"));
        var section = editor.Project.AddSection(SectionKind.Bridge);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Accent);
        editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 5), 0, 480, 90);
        var accent = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 5), 0, 120, 112);

        var proposal = AccentRealizer.Propose(editor.Project, section.Id);
        Assert.Equal(accent.Id, Assert.Single(proposal.Events).ExistingNoteEventId);

        editor.Execute(new UseAccentProposalCommand(section.Id));
        Assert.Equal(accent.Id, Assert.Single(Assert.Single(editor.Project.MusicalParts).NoteEventIds));
        Assert.Equal(2, editor.Project.NoteEvents.Count);
        editor.Undo();
        Assert.Equal(2, editor.Project.NoteEvents.Count);
    }

    [Fact]
    public void Proposal_RequiresRoleApprovedNotesAndDownbeats()
    {
        var project = SongProject.Create("Requirements");
        var section = project.AddSection(SectionKind.Verse);
        Assert.Throws<InvalidOperationException>(() => AccentRealizer.Propose(project, section.Id));
        project.SetSectionRole(section.Id, ArrangementRole.Accent);
        Assert.Throws<InvalidOperationException>(() => AccentRealizer.Propose(project, section.Id));
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 480, 240, 90);
        Assert.Throws<InvalidOperationException>(() => AccentRealizer.Propose(project, section.Id));
    }
}
