using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class PulseRealizationTests
{
    [Fact]
    public void Proposal_PlacesShortPulseHitsOnApprovedOnsets()
    {
        var project = SongProject.Create("Pulse");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.Pulse);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 0, 960, 75);
        var c4 = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 90);
        var g3 = project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3), 480, 240, 82);

        var proposal = PulseRealizer.Propose(project, section.Id);

        Assert.Equal("Chorus pulse", proposal.PartLabel);
        Assert.Equal(0, proposal.ReusedNoteCount);
        Assert.Collection(proposal.Events,
            first =>
            {
                Assert.Equal("C3", first.Pitch.ToDisplayString());
                Assert.Equal(c4.Id, first.SourceNoteEventId);
                Assert.Equal(0, first.StartTick);
                Assert.Equal(120, first.DurationTicks);
                Assert.Equal(90, first.Velocity);
                Assert.Null(first.ExistingNoteEventId);
            },
            second =>
            {
                Assert.Equal("C3", second.Pitch.ToDisplayString());
                Assert.Equal(g3.Id, second.SourceNoteEventId);
                Assert.Equal(480, second.StartTick);
                Assert.Equal(120, second.DurationTicks);
                Assert.Equal(82, second.Velocity);
            });
    }

    [Fact]
    public void Acceptance_CreatesOnePartAndIsExactlyReversible()
    {
        var editor = new ProjectEditor(SongProject.Create("Accepted pulse"));
        var section = editor.Project.AddSection(SectionKind.Verse);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Pulse);
        editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 96);
        editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3), 480, 480, 88);
        var originalIds = editor.Project.NoteEvents.Select(item => item.Id).ToList();

        editor.Execute(new UsePulseProposalCommand(section.Id));
        var createdPart = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal(ArrangementRole.Pulse, createdPart.Role);
        Assert.Null(createdPart.InstrumentProfileId);
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
    public void Acceptance_ReusesAnExistingPulseNoteWithoutDuplicatingIt()
    {
        var editor = new ProjectEditor(SongProject.Create("Existing pulse note"));
        var section = editor.Project.AddSection(SectionKind.Bridge);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Pulse);
        var c3 = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 80);

        var proposal = PulseRealizer.Propose(editor.Project, section.Id);
        Assert.Equal(c3.Id, Assert.Single(proposal.Events).ExistingNoteEventId);

        editor.Execute(new UsePulseProposalCommand(section.Id));
        Assert.Equal(c3.Id, Assert.Single(Assert.Single(editor.Project.MusicalParts).NoteEventIds));
        Assert.Single(editor.Project.NoteEvents);
        editor.Undo();
        Assert.Equal(c3.Id, Assert.Single(editor.Project.NoteEvents).Id);
    }

    [Fact]
    public void Proposal_RequiresRoleAndApprovedNotes()
    {
        var project = SongProject.Create("Requirements");
        var section = project.AddSection(SectionKind.Verse);
        Assert.Throws<InvalidOperationException>(() => PulseRealizer.Propose(project, section.Id));
        project.SetSectionRole(section.Id, ArrangementRole.Pulse);
        Assert.Throws<InvalidOperationException>(() => PulseRealizer.Propose(project, section.Id));
    }
}
