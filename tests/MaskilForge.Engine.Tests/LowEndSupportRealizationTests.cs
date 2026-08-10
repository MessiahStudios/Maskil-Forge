using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class LowEndSupportRealizationTests
{
    [Fact]
    public void Proposal_UsesLowestApprovedOnsetNotesInLowRegister()
    {
        var project = SongProject.Create("Low end");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.LowEndSupport);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 0, 960, 75);
        var c4 = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 90);
        var g3 = project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3), 480, 240, 82);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.B, Accidental.Natural, 3), 480, 480, 70);

        var proposal = LowEndSupportRealizer.Propose(project, section.Id);

        Assert.Equal("Chorus low-end support", proposal.PartLabel);
        Assert.Equal(0, proposal.ReusedNoteCount);
        Assert.Collection(proposal.Events,
            first =>
            {
                Assert.Equal("C2", first.Pitch.ToDisplayString());
                Assert.Equal(c4.Id, first.SourceNoteEventId);
                Assert.Equal(0, first.StartTick);
                Assert.Equal(480, first.DurationTicks);
                Assert.Equal(90, first.Velocity);
                Assert.Null(first.ExistingNoteEventId);
            },
            second =>
            {
                Assert.Equal("G2", second.Pitch.ToDisplayString());
                Assert.Equal(g3.Id, second.SourceNoteEventId);
                Assert.Equal(480, second.StartTick);
                Assert.Equal(240, second.DurationTicks);
                Assert.Equal(82, second.Velocity);
            });
    }

    [Fact]
    public void Acceptance_CreatesOnePartAndIsExactlyReversible()
    {
        var editor = new ProjectEditor(SongProject.Create("Accepted low end"));
        var section = editor.Project.AddSection(SectionKind.Verse);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.LowEndSupport);
        editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 96);
        editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3), 480, 480, 88);
        var originalIds = editor.Project.NoteEvents.Select(item => item.Id).ToList();

        editor.Execute(new UseLowEndSupportProposalCommand(section.Id));
        var createdPart = Assert.Single(editor.Project.MusicalParts);
        var realizedIds = createdPart.NoteEventIds.ToList();
        Assert.Equal(4, editor.Project.NoteEvents.Count);
        Assert.All(realizedIds, id => Assert.DoesNotContain(id, originalIds));

        editor.Undo();
        Assert.Empty(editor.Project.MusicalParts);
        Assert.Equal(originalIds, editor.Project.NoteEvents.Select(item => item.Id));
        editor.Redo();
        Assert.Equal(createdPart.Id, Assert.Single(editor.Project.MusicalParts).Id);
        Assert.Equal(realizedIds, Assert.Single(editor.Project.MusicalParts).NoteEventIds);
    }

    [Fact]
    public void Acceptance_ReusesAnExistingLowNoteWithoutDuplicatingIt()
    {
        var editor = new ProjectEditor(SongProject.Create("Existing low note"));
        var section = editor.Project.AddSection(SectionKind.Bridge);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.LowEndSupport);
        var c2 = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 2), 0, 480, 80);

        var proposal = LowEndSupportRealizer.Propose(editor.Project, section.Id);
        Assert.Equal(c2.Id, Assert.Single(proposal.Events).ExistingNoteEventId);

        editor.Execute(new UseLowEndSupportProposalCommand(section.Id));
        Assert.Equal(c2.Id, Assert.Single(Assert.Single(editor.Project.MusicalParts).NoteEventIds));
        Assert.Single(editor.Project.NoteEvents);
        editor.Undo();
        Assert.Equal(c2.Id, Assert.Single(editor.Project.NoteEvents).Id);
    }

    [Fact]
    public void Proposal_RequiresRoleAndApprovedNotes()
    {
        var project = SongProject.Create("Requirements");
        var section = project.AddSection(SectionKind.Verse);
        Assert.Throws<InvalidOperationException>(() => LowEndSupportRealizer.Propose(project, section.Id));
        project.SetSectionRole(section.Id, ArrangementRole.LowEndSupport);
        Assert.Throws<InvalidOperationException>(() => LowEndSupportRealizer.Propose(project, section.Id));
    }
}
