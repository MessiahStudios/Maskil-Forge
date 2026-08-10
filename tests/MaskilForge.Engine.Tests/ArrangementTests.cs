using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class ArrangementTests
{
    [Fact]
    public void SetSectionArrangementCommand_PreservesIdentityAcrossUndoRedo()
    {
        var project = SongProject.Create("Energy curve");
        var section = project.AddSection(SectionKind.Chorus);
        var editor = new ProjectEditor(project);

        editor.Execute(new SetSectionArrangementCommand(section.Id, SectionEnergy.Peak, SectionDensity.Full));
        var plan = Assert.Single(project.Arrangement);
        editor.Undo();
        Assert.Empty(project.Arrangement);
        editor.Redo();

        var restored = Assert.Single(project.Arrangement);
        Assert.Equal(plan.Id, restored.Id);
        Assert.Equal(SectionEnergy.Peak, restored.Energy);
        Assert.Equal(SectionDensity.Full, restored.Density);
    }

    [Fact]
    public void RemoveSectionCommand_UndoRestoresArrangementIdentity()
    {
        var project = SongProject.Create("Energy curve");
        var section = project.AddSection(SectionKind.Verse);
        var plan = project.SetSectionArrangement(section.Id, SectionEnergy.Gentle, SectionDensity.Light);
        var editor = new ProjectEditor(project);

        editor.Execute(new RemoveSectionCommand(section.Id));
        Assert.Empty(project.Arrangement);
        editor.Undo();

        Assert.Equal(plan.Id, Assert.Single(project.Arrangement).Id);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesSectionArrangement()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var project = SongProject.Create("Energy curve");
            var section = project.AddSection(SectionKind.Bridge);
            var plan = project.SetSectionArrangement(section.Id, SectionEnergy.Strong, SectionDensity.Sparse);
            var repository = new JsonFileProjectRepository(directory);

            await repository.SaveAsync(project);
            var restored = Assert.Single((await repository.LoadAsync(project.Id))!.Arrangement);

            Assert.Equal(plan.Id, restored.Id);
            Assert.Equal(section.Id, restored.SectionId);
            Assert.Equal(ArrangementProvenance.Manual, restored.Provenance);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Load_MigratesSchemaV15WithoutInventingArrangement()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, $"{projectId}.json"), $$"""
            {
              "id": "{{projectId}}", "schemaVersion": 15, "title": "Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": []
              },
              "sections": [], "tracks": [], "locks": [],
              "key": { "tonic": "C", "accidental": "Natural", "mode": "Major" }
            }
            """);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId);

            Assert.Equal(SchemaVersion.Current, loaded!.SchemaVersion);
            Assert.Empty(loaded.Arrangement);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
