using System.Text;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class EditableDemoWorkflowTests
{
    [Fact]
    public async Task VerticalSong_CanBeRealizedRevisedExportedAndReopened()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var editor = new ProjectEditor(SongProject.Create("Hear, revise, hear"));
            editor.Project.SetRawLyricDraft("A verse reaches for a chorus, then returns with new weight.");
            var sections = new[]
            {
                editor.Project.AddSection(SectionKind.Verse, "Verse 1"),
                editor.Project.AddSection(SectionKind.Chorus, "Chorus 1"),
                editor.Project.AddSection(SectionKind.Verse, "Verse 2"),
                editor.Project.AddSection(SectionKind.Chorus, "Final Chorus"),
            };
            var lyricLines = new[] { "I begin", "Carry me home", "I understand", "Carry me home again" };
            var roots = new[] { NoteLetter.C, NoteLetter.F, NoteLetter.A, NoteLetter.G };

            for (var index = 0; index < sections.Length; index++)
            {
                sections[index].AddLyricLine(lyricLines[index]);
                editor.Project.SetSectionArrangement(
                    sections[index].Id,
                    index == sections.Length - 1 ? SectionEnergy.Peak : SectionEnergy.Building,
                    index == sections.Length - 1 ? SectionDensity.Full : SectionDensity.Balanced);
                editor.Project.SetSectionRole(sections[index].Id, ArrangementRole.Harmony);
                editor.Project.AddHarmonyChord(
                    sections[index].Id,
                    new ChordSymbol(roots[index], Accidental.Natural, ChordQuality.Major),
                    new BeatPosition(1, 1, 0),
                    durationBars: 8);
                editor.Execute(new UseHarmonySupportProposalCommand(sections[index].Id));
            }

            Assert.Equal(4, editor.Project.MusicalParts.Count);
            Assert.All(sections, section => Assert.Contains(editor.Project.MusicalParts, part => part.SectionId == section.Id));
            var originalPart = editor.Project.MusicalParts[0];
            var originalNote = editor.Project.NoteEvents.Single(note => note.Id == originalPart.NoteEventIds[0]);

            editor.Execute(new SetNoteEventCommand(
                originalNote.Id,
                originalNote.Pitch,
                originalNote.StartTick,
                originalNote.DurationTicks / 2,
                84));
            editor.Execute(new SetMusicalPartCommand(originalPart.Id, "Revised verse harmony", originalPart.NoteEventIds));
            Assert.Equal(originalNote.Id, editor.Project.NoteEvents.Single(note => note.Id == originalNote.Id).Id);
            Assert.Equal("Revised verse harmony", editor.Project.MusicalParts.Single(part => part.Id == originalPart.Id).Label);

            editor.Undo();
            Assert.Equal(originalPart.Label, editor.Project.MusicalParts.Single(part => part.Id == originalPart.Id).Label);
            editor.Redo();
            Assert.Equal("Revised verse harmony", editor.Project.MusicalParts.Single(part => part.Id == originalPart.Id).Label);

            var midi = MidiFileExporter.Export(editor.Project);
            Assert.Equal("MThd", Encoding.ASCII.GetString(midi, 0, 4));

            var repository = new JsonFileProjectRepository(directory);
            await repository.SaveAsync(editor.Project);
            var reopened = await repository.LoadAsync(editor.Project.Id);

            Assert.NotNull(reopened);
            Assert.Equal(sections.Select(section => section.Id), reopened.Sections.Select(section => section.Id));
            Assert.Equal(editor.Project.NoteEvents.Select(note => note.Id), reopened.NoteEvents.Select(note => note.Id));
            Assert.Equal(editor.Project.MusicalParts.Select(part => part.Id), reopened.MusicalParts.Select(part => part.Id));
            Assert.Equal("Revised verse harmony", reopened.MusicalParts.Single(part => part.Id == originalPart.Id).Label);
            Assert.Equal(84, reopened.NoteEvents.Single(note => note.Id == originalNote.Id).Velocity);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
