using System.Text.Json;
using System.Text.Json.Nodes;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class PortableProjectExporterTests
{
    [Fact]
    public void Export_PreservesVersionIdentityAndArtistDecisionsDeterministically()
    {
        var project = SongProject.Create("Portable shadows");
        project.SetGenre(SongGenre.Cinematic);
        var section = project.AddSection(SectionKind.PreChorus);
        section.AddLyricLine("Light reaches the edge.");
        project.SetSectionRole(section.Id, ArrangementRole.Texture);

        var first = PortableProjectExporter.Export(project);
        var second = PortableProjectExporter.Export(project);
        using var document = JsonDocument.Parse(first);
        var root = document.RootElement;

        Assert.Equal(first, second);
        Assert.Equal(SchemaVersion.Current.Value, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(project.Id.ToString(), root.GetProperty("id").GetString());
        Assert.Equal("Portable shadows", root.GetProperty("title").GetString());
        Assert.Equal("Cinematic", root.GetProperty("genre").GetString());
        Assert.Equal("PreChorus", root.GetProperty("sections")[0].GetProperty("kind").GetString());
        Assert.Equal("Texture", root.GetProperty("arrangementRoles")[0].GetProperty("role").GetString());
    }

    [Fact]
    public void Import_RoundTripsTheCompleteCurrentProject()
    {
        var project = SongProject.Create("Round-trip song");
        project.SetArtist("Portable Artist");
        var section = project.AddSection(SectionKind.Chorus);
        section.AddLyricLine("Carry this song with me.");

        var imported = PortableProjectImporter.Import(System.Text.Encoding.UTF8.GetString(PortableProjectExporter.Export(project)));

        Assert.Equal(project.Id, imported.Id);
        Assert.Equal(project.SchemaVersion, imported.SchemaVersion);
        Assert.Equal("Round-trip song", imported.Title);
        Assert.Equal("Portable Artist", imported.Artist);
        Assert.Equal("Carry this song with me.", imported.Sections.Single().LyricLines.Single().Text);
    }

    [Fact]
    public void Import_MigratesSupportedOlderProjectSchemas()
    {
        var project = SongProject.Create("Migrating song");
        project.AddSection(SectionKind.Verse);
        var document = JsonNode.Parse(PortableProjectExporter.Export(project))!.AsObject();
        document["schemaVersion"] = 20;
        foreach (var section in document["sections"]!.AsArray().OfType<JsonObject>())
            section.Remove("structuralFunction");

        var imported = PortableProjectImporter.Import(document.ToJsonString());

        Assert.Equal(SchemaVersion.Current, imported.SchemaVersion);
        Assert.Equal(StructuralFunction.Unspecified, imported.Sections.Single().StructuralFunction);
    }

    [Fact]
    public void Import_RejectsMalformedAndNewerProjectDocuments()
    {
        Assert.Throws<CorruptProjectException>(() => PortableProjectImporter.Import("{not-json"));

        var document = JsonNode.Parse(PortableProjectExporter.Export(SongProject.Create("Future song")))!.AsObject();
        document["schemaVersion"] = SchemaVersion.Current.Value + 1;
        var exception = Assert.Throws<UnsupportedProjectSchemaException>(() =>
            PortableProjectImporter.Import(document.ToJsonString()));
        Assert.Equal(SchemaVersion.Current.Value + 1, exception.Version);
    }

    [Fact]
    public async Task RepositoryImport_PersistsANewIdentityAndNeverOverwritesACollision()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-portable-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Imported original");
            await repository.ImportAsync(project);
            Assert.Equal("Imported original", (await repository.LoadAsync(project.Id))!.Title);

            var document = JsonNode.Parse(PortableProjectExporter.Export(project))!.AsObject();
            document["title"] = "Attempted overwrite";
            var collision = PortableProjectImporter.Import(document.ToJsonString());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ImportAsync(collision));
            Assert.Contains("Nothing was overwritten", exception.Message);
            Assert.Equal("Imported original", (await repository.LoadAsync(project.Id))!.Title);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
