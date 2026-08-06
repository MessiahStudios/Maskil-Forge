using MaskilForge.Domain;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class JsonPersistenceTests
{
    [Fact]
    public async Task SaveAndLoad_PreservesIdentifiersOrderingAndLyrics()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MaskilForge.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var savingRepository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Round Trip");
            project.SetArtist("Maskil Artist");
            project.SetGenre(SongGenre.Alternative);
            project.SetDescription("A persistence proof.");
            project.SetRawLyricDraft("Unstructured source words that must remain intact.");
            project.SetTempo(84);
            project.SetTimeSignature(6, 8);
            var verse = project.AddSection(SectionKind.Verse);
            var verseLine = verse.AddLyricLine("I walked through shadows");
            var chorus = project.AddSection(SectionKind.Chorus);
            var chorusLine = chorus.AddLyricLine("You brought me home");

            await savingRepository.SaveAsync(project, CancellationToken.None);

            // A new repository instance represents closing the application and reloading from disk.
            var loadingRepository = new JsonFileProjectRepository(directory);
            var loaded = await loadingRepository.LoadAsync(project.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(project.Id, loaded.Id);
            Assert.Equal([verse.Id, chorus.Id], loaded.Sections.Select(section => section.Id));
            Assert.Equal(verseLine.Id, loaded.Sections[0].LyricLines[0].Id);
            Assert.Equal("I walked through shadows", loaded.Sections[0].LyricLines[0].Text);
            Assert.Equal(chorusLine.Id, loaded.Sections[1].LyricLines[0].Id);
            Assert.Equal("You brought me home", loaded.Sections[1].LyricLines[0].Text);
            Assert.Equal("Maskil Artist", loaded.Artist);
            Assert.Equal(SongGenre.Alternative, loaded.Genre);
            Assert.Equal("A persistence proof.", loaded.Description);
            Assert.Equal("Unstructured source words that must remain intact.", loaded.RawLyricDraft);
            Assert.Equal(84, loaded.Tempo.BeatsPerMinute);
            Assert.Equal((6, 8), (loaded.TimeSignature.Numerator, loaded.TimeSignature.Denominator));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ListAsync_ReturnsProjectSummariesWithoutRequiringKnownIds()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var draft = SongProject.Create("Raw Draft");
            draft.SetRawLyricDraft("A loose lyric idea");
            var structured = SongProject.Create("Structured Song");
            structured.AddSection(SectionKind.Verse);
            await repository.SaveAsync(draft, CancellationToken.None);
            await repository.SaveAsync(structured, CancellationToken.None);

            var summaries = await repository.ListAsync(CancellationToken.None);

            Assert.Equal(2, summaries.Count);
            Assert.Contains(summaries, item => item.Id == draft.Id && item.HasRawLyrics && item.SectionCount == 0);
            Assert.Contains(summaries, item => item.Id == structured.Id && !item.HasRawLyrics && item.SectionCount == 1);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task MoveToTrash_RemovesProjectFromLibraryWithoutDestroyingJson()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Recoverable Song");
            await repository.SaveAsync(project, CancellationToken.None);

            Assert.True(await repository.MoveToTrashAsync(project.Id, CancellationToken.None));
            Assert.Null(await repository.LoadAsync(project.Id, CancellationToken.None));
            Assert.Empty(await repository.ListAsync(CancellationToken.None));
            Assert.Single(Directory.EnumerateFiles(Path.Combine(directory, "trash"), "*.json"));

            var trash = await repository.ListTrashAsync(CancellationToken.None);
            Assert.Single(trash);
            Assert.Equal(project.Id, trash[0].Id);

            Assert.True(await repository.RestoreFromTrashAsync(project.Id, CancellationToken.None));
            Assert.NotNull(await repository.LoadAsync(project.Id, CancellationToken.None));
            Assert.Empty(await repository.ListTrashAsync(CancellationToken.None));

            Assert.True(await repository.MoveToTrashAsync(project.Id, CancellationToken.None));
            Assert.True(await repository.PermanentlyDeleteAsync(project.Id, CancellationToken.None));
            Assert.Empty(await repository.ListTrashAsync(CancellationToken.None));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(directory, "trash"), "*.json"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
