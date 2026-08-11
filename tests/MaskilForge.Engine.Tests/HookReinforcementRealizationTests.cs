using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class HookReinforcementRealizationTests
{
    [Fact]
    public void Proposal_EmphasizesHighestApprovedNotesWithBeatCappedHits()
    {
        var project = SongProject.Create("Hook");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.HookReinforcement);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 960, 80);
        var e4 = project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 0, 960, 90);
        var g4 = project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 4), 480, 240, 85);

        var proposal = HookReinforcementRealizer.Propose(project, section.Id);

        Assert.Equal("Chorus hook reinforcement", proposal.PartLabel);
        Assert.Equal(1, proposal.ReusedNoteCount);
        Assert.Collection(proposal.Events,
            first =>
            {
                Assert.Equal("E4", first.Pitch.ToDisplayString());
                Assert.Equal(e4.Id, first.SourceNoteEventId);
                Assert.Equal(0, first.StartTick);
                Assert.Equal(480, first.DurationTicks);
                Assert.Equal(108, first.Velocity);
                Assert.Null(first.ExistingNoteEventId);
            },
            second =>
            {
                Assert.Equal("G4", second.Pitch.ToDisplayString());
                Assert.Equal(g4.Id, second.SourceNoteEventId);
                Assert.Equal(480, second.StartTick);
                Assert.Equal(240, second.DurationTicks);
                Assert.Equal(108, second.Velocity);
                Assert.Equal(g4.Id, second.ExistingNoteEventId);
            });
    }

    [Fact]
    public void Acceptance_CreatesOnePartAndIsExactlyReversible()
    {
        var editor = new ProjectEditor(SongProject.Create("Accepted hook"));
        var section = editor.Project.AddSection(SectionKind.Verse);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.HookReinforcement);
        editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 5), 0, 960, 90);
        editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 4), 480, 960, 88);
        var originalIds = editor.Project.NoteEvents.Select(item => item.Id).ToList();

        editor.Execute(new UseHookReinforcementProposalCommand(section.Id));
        var createdPart = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal(ArrangementRole.HookReinforcement, createdPart.Role);
        Assert.Equal(2, createdPart.NoteEventIds.Count);
        Assert.Equal(4, editor.Project.NoteEvents.Count);
        Assert.All(createdPart.NoteEventIds, id => Assert.DoesNotContain(id, originalIds));

        editor.Undo();
        Assert.Empty(editor.Project.MusicalParts);
        Assert.Equal(originalIds, editor.Project.NoteEvents.Select(item => item.Id));
        editor.Redo();
        Assert.Equal(createdPart.Id, Assert.Single(editor.Project.MusicalParts).Id);
        Assert.Equal(createdPart.NoteEventIds, Assert.Single(editor.Project.MusicalParts).NoteEventIds);
    }

    [Fact]
    public void Acceptance_ReusesAnExistingHookNoteWithoutDuplicatingIt()
    {
        var editor = new ProjectEditor(SongProject.Create("Existing hook note"));
        var section = editor.Project.AddSection(SectionKind.Bridge);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.HookReinforcement);
        var c5 = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 5), 0, 480, 110);

        var proposal = HookReinforcementRealizer.Propose(editor.Project, section.Id);
        Assert.Equal(c5.Id, Assert.Single(proposal.Events).ExistingNoteEventId);

        editor.Execute(new UseHookReinforcementProposalCommand(section.Id));
        Assert.Equal(c5.Id, Assert.Single(Assert.Single(editor.Project.MusicalParts).NoteEventIds));
        Assert.Single(editor.Project.NoteEvents);
        editor.Undo();
        Assert.Equal(c5.Id, Assert.Single(editor.Project.NoteEvents).Id);
    }

    [Fact]
    public void Proposal_RequiresRoleAndApprovedNotes()
    {
        var project = SongProject.Create("Requirements");
        var section = project.AddSection(SectionKind.Verse);
        Assert.Throws<InvalidOperationException>(() => HookReinforcementRealizer.Propose(project, section.Id));
        project.SetSectionRole(section.Id, ArrangementRole.HookReinforcement);
        Assert.Throws<InvalidOperationException>(() => HookReinforcementRealizer.Propose(project, section.Id));
    }
}
