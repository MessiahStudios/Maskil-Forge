using MaskilForge.Api;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class EditorStateWorkflowTests
{
    [Fact]
    public async Task DuplicatingASavedSong_CreatesAnIndependentNamedCopyWithoutChangingCreativeIdentities()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var original = await workspace.CreateAsync("Branching Song", CancellationToken.None);
            original.Project.SetArtist("Test Artist");
            original.Project.SetGenre(SongGenre.Alternative);
            var verse = original.Project.AddSection(SectionKind.Verse);
            var line = verse.AddLyricLine("Keep the words, branch the project.");
            await workspace.SaveAsync(original, CancellationToken.None);

            var firstCopy = await workspace.DuplicateAsync(original.Project.Id, CancellationToken.None);
            var secondCopy = await workspace.DuplicateAsync(original.Project.Id, CancellationToken.None);

            Assert.NotNull(firstCopy);
            Assert.NotNull(secondCopy);
            Assert.NotEqual(original.Project.Id, firstCopy.Project.Id);
            Assert.NotEqual(firstCopy.Project.Id, secondCopy.Project.Id);
            Assert.Equal("Branching Song Copy", firstCopy.Project.Title);
            Assert.Equal("Branching Song Copy 2", secondCopy.Project.Title);
            Assert.Equal(original.Project.Artist, firstCopy.Project.Artist);
            Assert.Equal(original.Project.Genre, firstCopy.Project.Genre);
            Assert.Equal(verse.Id, firstCopy.Project.Sections.Single().Id);
            Assert.Equal(line.Id, firstCopy.Project.Sections.Single().LyricLines.Single().Id);
            Assert.Equal(line.Text, firstCopy.Project.Sections.Single().LyricLines.Single().Text);
            Assert.Equal("Branching Song", (await repository.LoadAsync(original.Project.Id))!.Title);
            Assert.Equal("Branching Song Copy", (await repository.LoadAsync(firstCopy.Project.Id))!.Title);
            Assert.Equal("Branching Song Copy 2", (await repository.LoadAsync(secondCopy.Project.Id))!.Title);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task SynchronizingUnchangedMeter_PreservesUndoForARealizedMusicalPart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var workspace = new ProjectWorkspace(new JsonFileProjectRepository(directory));
            var editor = await workspace.CreateAsync("Undo part", CancellationToken.None);
            var verse = editor.Project.AddSection(SectionKind.Verse);
            editor.Project.SetSectionRole(verse.Id, ArrangementRole.LowEndSupport);
            editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 96);
            editor.Execute(new UseLowEndSupportProposalCommand(verse.Id));

            var undone = await workspace.UseAsync(
                editor.Project.Id,
                editor.Project,
                current => current.Undo(),
                CancellationToken.None);

            Assert.True(undone);
            Assert.Empty(editor.Project.MusicalParts);
            Assert.Single(editor.Project.NoteEvents);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

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
