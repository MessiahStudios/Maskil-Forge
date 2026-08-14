using System.Text.Json;
using MaskilForge.Domain;
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
}
