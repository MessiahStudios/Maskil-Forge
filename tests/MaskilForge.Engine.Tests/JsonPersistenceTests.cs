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
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Round Trip");
            project.SetTempo(84);
            project.SetTimeSignature(6, 8);
            var verse = project.AddSection(SectionKind.Verse, "Opening Verse");
            var line = verse.AddLyricLine("Understand the words.");
            var chorus = project.AddSection(SectionKind.Chorus);

            await repository.SaveAsync(project, CancellationToken.None);
            var loaded = await repository.LoadAsync(project.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(project.Id, loaded.Id);
            Assert.Equal([verse.Id, chorus.Id], loaded.Sections.Select(section => section.Id));
            Assert.Equal(line.Id, loaded.Sections[0].LyricLines[0].Id);
            Assert.Equal("Understand the words.", loaded.Sections[0].LyricLines[0].Text);
            Assert.Equal(84, loaded.Tempo.BeatsPerMinute);
            Assert.Equal((6, 8), (loaded.TimeSignature.Numerator, loaded.TimeSignature.Denominator));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
