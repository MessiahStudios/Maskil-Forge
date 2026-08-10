using MaskilForge.Api;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class EditorStateWorkflowTests
{
    [Fact]
    public async Task SynchronizingEditorBeforeSectionCommand_PreservesUnsavedLyrics()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var workspace = new ProjectWorkspace(new JsonFileProjectRepository(directory));
            var editor = await workspace.CreateAsync("Untitled Song", CancellationToken.None);
            var verse = editor.Project.AddSection(SectionKind.Verse);
            var line = verse.AddLyricLine("I have an idea");
            var note = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 96);

            var clientSnapshot = new SongProject(
                editor.Project.Id,
                editor.Project.SchemaVersion,
                "Ashes & Redemption",
                new SongTimeline(
                    TimelineResolution.TicksPerQuarterNote,
                    new TempoMap(editor.Project.Timeline.TempoMap.Events),
                    new TimeSignatureMap(editor.Project.Timeline.TimeSignatureMap.Events),
                    [new SectionPlacement(verse.Id, new MusicalPosition(1, 1, 0), 12)]),
                editor.Project.Sections,
                editor.Project.Tracks,
                "Test Artist",
                SongGenre.Alternative,
                "An unsaved creative idea.");

            var synced = await workspace.SyncAsync(clientSnapshot, CancellationToken.None);
            synced!.Execute(new AddSectionCommand(SectionKind.Chorus));

            Assert.Equal("Ashes & Redemption", synced.Project.Title);
            Assert.Equal([SectionKind.Verse, SectionKind.Chorus], synced.Project.Sections.Select(section => section.Kind));
            Assert.Equal(line.Id, synced.Project.Sections[0].LyricLines[0].Id);
            Assert.Equal("I have an idea", synced.Project.Sections[0].LyricLines[0].Text);
            Assert.Equal(12, synced.Project.Timeline.FindSection(verse.Id).DurationBars);
            Assert.Equal(13, synced.Project.Timeline.FindSection(synced.Project.Sections[1].Id).Start.Bar);

            await workspace.SaveAsync(synced, CancellationToken.None);
            var reopened = await new JsonFileProjectRepository(directory).LoadAsync(synced.Project.Id, CancellationToken.None);
            Assert.Equal(line.Id, reopened!.Sections[0].LyricLines[0].Id);
            Assert.Equal("I have an idea", reopened.Sections[0].LyricLines[0].Text);
            Assert.Equal(12, reopened.Timeline.FindSection(verse.Id).DurationBars);
            Assert.Equal(note.Id, Assert.Single(reopened.NoteEvents).Id);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
