using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class MusicalPartTests
{
    [Fact]
    public void Commands_CreateArtistAuthoredPartWithStableUndoRedo()
    {
        var editor = new ProjectEditor(SongProject.Create("Parts"));
        var section = editor.Project.AddSection(SectionKind.Chorus);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        var c4 = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 96);
        var g4 = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 4), 0, 480, 88);

        editor.Execute(new AddMusicalPartCommand(section.Id, ArrangementRole.Foundation, "Chorus foundation", [c4.Id, g4.Id]));
        var created = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal("Chorus foundation", created.Label);
        Assert.Equal(ArrangementRole.Foundation, created.Role);
        Assert.Equal([c4.Id, g4.Id], created.NoteEventIds);
        Assert.Null(created.InstrumentProfileId);

        editor.Undo();
        Assert.Empty(editor.Project.MusicalParts);
        Assert.Equal(2, editor.Project.NoteEvents.Count);
        editor.Redo();
        Assert.Equal(created.Id, Assert.Single(editor.Project.MusicalParts).Id);
    }

    [Fact]
    public void Commands_EditPartNameAndMembershipWithStableUndoRedo()
    {
        var editor = new ProjectEditor(SongProject.Create("Edit parts"));
        var section = editor.Project.AddSection(SectionKind.Chorus);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        var c4 = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 96);
        var g4 = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 4), 480, 480, 88);
        var part = editor.Project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Old name", [c4.Id]);

        editor.Execute(new SetMusicalPartCommand(part.Id, "Revised foundation", [c4.Id, g4.Id]));
        var revised = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal(part.Id, revised.Id);
        Assert.Equal("Revised foundation", revised.Label);
        Assert.Equal([c4.Id, g4.Id], revised.NoteEventIds);

        editor.Undo();
        var restored = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal(part.Id, restored.Id);
        Assert.Equal("Old name", restored.Label);
        Assert.Equal([c4.Id], restored.NoteEventIds);

        editor.Redo();
        Assert.Equal("Revised foundation", Assert.Single(editor.Project.MusicalParts).Label);
    }

    [Fact]
    public void Commands_AssignAndClearCatalogInstrumentWithStableUndoRedo()
    {
        var editor = new ProjectEditor(SongProject.Create("Assign cello"));
        var section = editor.Project.AddSection(SectionKind.Chorus);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        var note = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 90);
        editor.Execute(new AddMusicalPartCommand(section.Id, ArrangementRole.Foundation, "Chorus foundation", [note.Id], "cello"));
        var assigned = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal("cello", assigned.InstrumentProfileId);

        editor.Undo();
        Assert.Empty(editor.Project.MusicalParts);
        editor.Redo();
        Assert.Equal("cello", Assert.Single(editor.Project.MusicalParts).InstrumentProfileId);

        editor.Execute(new SetMusicalPartCommand(assigned.Id, assigned.Label, assigned.NoteEventIds, "acoustic-guitar"));
        Assert.Equal("acoustic-guitar", Assert.Single(editor.Project.MusicalParts).InstrumentProfileId);
        editor.Undo();
        Assert.Equal("cello", Assert.Single(editor.Project.MusicalParts).InstrumentProfileId);
        editor.Redo();
        Assert.Equal("acoustic-guitar", Assert.Single(editor.Project.MusicalParts).InstrumentProfileId);

        editor.Execute(new SetMusicalPartCommand(assigned.Id, assigned.Label, assigned.NoteEventIds, null));
        Assert.Null(Assert.Single(editor.Project.MusicalParts).InstrumentProfileId);
        editor.Undo();
        Assert.Equal("acoustic-guitar", Assert.Single(editor.Project.MusicalParts).InstrumentProfileId);
    }

    [Fact]
    public void Commands_RejectUnknownOrMalformedInstrumentIds()
    {
        var editor = new ProjectEditor(SongProject.Create("Unknown instrument"));
        var section = editor.Project.AddSection(SectionKind.Verse);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Harmony);
        var note = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 0, 480, 80);

        var unknown = Assert.Throws<ArgumentException>(() => editor.Execute(
            new AddMusicalPartCommand(section.Id, ArrangementRole.Harmony, "Verse harmony", [note.Id], "synth-pad")));
        Assert.Contains("synth-pad", unknown.Message);
        Assert.Empty(editor.Project.MusicalParts);

        var malformed = Assert.Throws<ArgumentException>(() => editor.Execute(
            new AddMusicalPartCommand(section.Id, ArrangementRole.Harmony, "Verse harmony", [note.Id], "Cello")));
        Assert.Contains("catalog slug", malformed.Message);
        Assert.Empty(editor.Project.MusicalParts);
    }

    [Fact]
    public void Commands_AssignWaveTwoViolinWithoutRetargeting()
    {
        var editor = new ProjectEditor(SongProject.Create("Wave two violin"));
        var section = editor.Project.AddSection(SectionKind.Verse);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Countermelody);
        var note = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 0, 480, 80);

        editor.Execute(new AddMusicalPartCommand(
            section.Id, ArrangementRole.Countermelody, "Verse violin", [note.Id], "violin"));

        var part = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal("violin", part.InstrumentProfileId);
        Assert.Equal("Verse violin", part.Label);
        Assert.Empty(editor.Project.ExpressionCurves);
    }

    [Fact]
    public void Commands_AllowAnInstrumentThatDoesNotCoverThePartJob()
    {
        var editor = new ProjectEditor(SongProject.Create("Artist authority"));
        var section = editor.Project.AddSection(SectionKind.Chorus);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Harmony);
        var note = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 88);

        editor.Execute(new AddMusicalPartCommand(section.Id, ArrangementRole.Harmony, "Chorus harmony", [note.Id], "drum-kit"));

        var part = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal("drum-kit", part.InstrumentProfileId);
        Assert.Equal(ArrangementRole.Harmony, part.Role);
        Assert.DoesNotContain(ArrangementRole.Harmony, InstrumentProfileCatalogLoader.Current.Find("drum-kit").Roles);
    }

    [Fact]
    public void PartProtectsItsRoleAndNotesUntilPartIsRemoved()
    {
        var project = SongProject.Create("Part references");
        var section = project.AddSection(SectionKind.Verse);
        project.SetSectionRole(section.Id, ArrangementRole.Pulse);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 240, 80);
        var part = project.AddMusicalPart(section.Id, ArrangementRole.Pulse, "Verse pulse", [note.Id]);

        Assert.Throws<InvalidOperationException>(() => project.RemoveSectionRole(section.Id, ArrangementRole.Pulse));
        Assert.Throws<InvalidOperationException>(() => project.RemoveNoteEvent(note.Id));

        project.RemoveMusicalPart(part.Id);
        Assert.Single(project.NoteEvents);
        project.RemoveNoteEvent(note.Id);
        project.RemoveSectionRole(section.Id, ArrangementRole.Pulse);
    }

    [Fact]
    public void PartProtectsSectionTimingUntilThePartIsRemoved()
    {
        var project = SongProject.Create("Section part");
        var section = project.AddSection(SectionKind.Bridge);
        project.SetSectionRole(section.Id, ArrangementRole.Texture);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 0, 960, 70);
        var part = project.AddMusicalPart(section.Id, ArrangementRole.Texture, "Bridge texture", [note.Id]);

        Assert.Throws<InvalidOperationException>(() => project.SetSectionDuration(section.Id, 4));
        Assert.Throws<InvalidOperationException>(() => project.SetTimeSignature(3, 4));
        Assert.Throws<InvalidOperationException>(() => project.RemoveSection(section.Id));

        project.RemoveMusicalPart(part.Id);
        project.SetSectionDuration(section.Id, 4);
        Assert.Equal(4, project.Timeline.FindSection(section.Id).DurationBars);
        Assert.Equal(note.Id, Assert.Single(project.NoteEvents).Id);
    }

    [Fact]
    public async Task Schema18_MigratesToEmptyMusicalPartsWithoutInventingAssignments()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var project = SongProject.Create("Schema 18");
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter());
            var json = JsonSerializer.SerializeToNode(project, options)!.AsObject();
            json["schemaVersion"] = 18;
            json.Remove("musicalParts");
            await File.WriteAllTextAsync(Path.Combine(directory, $"{project.Id}.json"), json.ToJsonString(options));

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(project.Id);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            Assert.Empty(loaded.MusicalParts);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Schema29_MigratesExistingPartsToUnassignedInstrumentsWithoutInventingThem()
    {
        var project = SongProject.Create("Schema 29 parts");
        var section = project.AddSection(SectionKind.Verse);
        project.SetSectionRole(section.Id, ArrangementRole.Pulse);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 96);
        project.AddMusicalPart(section.Id, ArrangementRole.Pulse, "Verse pulse", [note.Id], "cello");
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        document["schemaVersion"] = 29;
        foreach (var part in document["musicalParts"]!.AsArray().OfType<JsonObject>())
            part.Remove("instrumentProfileId");

        var inspected = PortableProjectImporter.Inspect(document.ToJsonString());

        Assert.Equal(29, inspected.SourceSchemaVersion);
        Assert.Equal(SchemaVersion.Current.Value, inspected.Project.SchemaVersion.Value);
        Assert.Null(Assert.Single(inspected.Project.MusicalParts).InstrumentProfileId);
        Assert.Equal("Verse pulse", Assert.Single(inspected.Project.MusicalParts).Label);
    }

    [Fact]
    public async Task SaveLoad_PreservesAssignedCatalogInstrument()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var project = SongProject.Create("Assigned cello");
            var section = project.AddSection(SectionKind.Chorus);
            project.SetSectionRole(section.Id, ArrangementRole.Foundation);
            var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 960, 90);
            var part = project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Chorus foundation", [note.Id], "cello");
            await new JsonFileProjectRepository(directory).SaveAsync(project);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(project.Id);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            var restored = Assert.Single(loaded.MusicalParts);
            Assert.Equal(part.Id, restored.Id);
            Assert.Equal("cello", restored.InstrumentProfileId);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
