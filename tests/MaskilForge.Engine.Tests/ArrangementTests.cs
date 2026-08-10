using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class ArrangementTests
{
    [Fact]
    public void SetSectionRoleCommand_PreservesIdentityAcrossUndoRedo()
    {
        var project = SongProject.Create("Roles");
        var section = project.AddSection(SectionKind.Chorus);
        var editor = new ProjectEditor(project);

        editor.Execute(new SetSectionRoleCommand(section.Id, ArrangementRole.HookReinforcement, true));
        var assignment = Assert.Single(project.ArrangementRoles);
        editor.Undo();
        Assert.Empty(project.ArrangementRoles);
        editor.Redo();

        Assert.Equal(assignment.Id, Assert.Single(project.ArrangementRoles).Id);
    }

    [Fact]
    public void RemoveSectionCommand_UndoRestoresRoleAssignments()
    {
        var project = SongProject.Create("Roles");
        var section = project.AddSection(SectionKind.Bridge);
        var assignment = project.SetSectionRole(section.Id, ArrangementRole.Transition);
        var editor = new ProjectEditor(project);

        editor.Execute(new RemoveSectionCommand(section.Id));
        Assert.Empty(project.ArrangementRoles);
        editor.Undo();

        Assert.Equal(assignment.Id, Assert.Single(project.ArrangementRoles).Id);
    }

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
            var role = project.SetSectionRole(section.Id, ArrangementRole.Texture);
            var repository = new JsonFileProjectRepository(directory);

            await repository.SaveAsync(project);
            var restored = Assert.Single((await repository.LoadAsync(project.Id))!.Arrangement);

            Assert.Equal(plan.Id, restored.Id);
            Assert.Equal(section.Id, restored.SectionId);
            Assert.Equal(ArrangementProvenance.Manual, restored.Provenance);
            var restoredRole = Assert.Single((await repository.LoadAsync(project.Id))!.ArrangementRoles);
            Assert.Equal(role.Id, restoredRole.Id);
            Assert.Equal(ArrangementRole.Texture, restoredRole.Role);
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

    [Fact]
    public async Task Load_MigratesSchemaV16WithoutInventingRoles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, $"{projectId}.json"), $$"""
            {
              "id": "{{projectId}}", "schemaVersion": 16, "title": "Role Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": []
              },
              "sections": [], "tracks": [], "locks": [], "arrangement": [],
              "key": { "tonic": "C", "accidental": "Natural", "mode": "Major" }
            }
            """);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId);

            Assert.Equal(SchemaVersion.Current, loaded!.SchemaVersion);
            Assert.Empty(loaded.ArrangementRoles);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
