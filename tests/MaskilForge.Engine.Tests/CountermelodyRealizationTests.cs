using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class CountermelodyRealizationTests
{
    [Fact]
    public void Proposal_FollowsSecondHighestApprovedNotesAsSofterResponse()
    {
        var project = SongProject.Create("Countermelody");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.Countermelody);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 90);
        var e4 = project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 0, 480, 92);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 4), 0, 480, 95);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.A, Accidental.Natural, 4), 480, 240, 88);
        var d4 = project.AddNoteEvent(new RegisteredPitch(NoteLetter.D, Accidental.Natural, 4), 480, 240, 80);

        var proposal = CountermelodyRealizer.Propose(project, section.Id);

        Assert.Equal("Chorus countermelody", proposal.PartLabel);
        Assert.Equal(0, proposal.ReusedNoteCount);
        Assert.Collection(proposal.Events,
            first =>
            {
                Assert.Equal("E4", first.Pitch.ToDisplayString());
                Assert.Equal(e4.Id, first.SourceNoteEventId);
                Assert.Equal(0, first.StartTick);
                Assert.Equal(480, first.DurationTicks);
                Assert.Equal(84, first.Velocity);
                Assert.Null(first.ExistingNoteEventId);
            },
            second =>
            {
                Assert.Equal("D4", second.Pitch.ToDisplayString());
                Assert.Equal(d4.Id, second.SourceNoteEventId);
                Assert.Equal(480, second.StartTick);
                Assert.Equal(240, second.DurationTicks);
                Assert.Equal(84, second.Velocity);
            });
    }

    [Fact]
    public void Acceptance_CreatesOnePartAndIsExactlyReversible()
    {
        var editor = new ProjectEditor(SongProject.Create("Accepted countermelody"));
        var section = editor.Project.AddSection(SectionKind.Verse);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Countermelody);
        editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 90);
        editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 0, 480, 95);
        var originalIds = editor.Project.NoteEvents.Select(item => item.Id).ToList();

        editor.Execute(new UseCountermelodyProposalCommand(section.Id));
        var createdPart = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal(ArrangementRole.Countermelody, createdPart.Role);
        Assert.Single(createdPart.NoteEventIds);
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
    public void Acceptance_ReusesAnExistingResponseNoteWithoutDuplicatingIt()
    {
        var editor = new ProjectEditor(SongProject.Create("Existing response note"));
        var section = editor.Project.AddSection(SectionKind.Bridge);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Countermelody);
        editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 4), 0, 480, 100);
        var response = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 0, 480, 84);

        var proposal = CountermelodyRealizer.Propose(editor.Project, section.Id);
        Assert.Equal(response.Id, Assert.Single(proposal.Events).ExistingNoteEventId);

        editor.Execute(new UseCountermelodyProposalCommand(section.Id));
        Assert.Equal(response.Id, Assert.Single(Assert.Single(editor.Project.MusicalParts).NoteEventIds));
        Assert.Equal(2, editor.Project.NoteEvents.Count);
        editor.Undo();
        Assert.Equal(2, editor.Project.NoteEvents.Count);
    }

    [Fact]
    public void Proposal_RequiresRoleApprovedNotesAndStackedOnsets()
    {
        var project = SongProject.Create("Requirements");
        var section = project.AddSection(SectionKind.Verse);
        Assert.Throws<InvalidOperationException>(() => CountermelodyRealizer.Propose(project, section.Id));
        project.SetSectionRole(section.Id, ArrangementRole.Countermelody);
        Assert.Throws<InvalidOperationException>(() => CountermelodyRealizer.Propose(project, section.Id));
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 90);
        Assert.Throws<InvalidOperationException>(() => CountermelodyRealizer.Propose(project, section.Id));
    }
}
