using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class MidiEventTests
{
    [Fact]
    public void NoteEvent_ValidatesMidiBoundaries()
    {
        var pitch = new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => new NoteEvent(NoteEventId.New(), pitch, -1, 480, 96));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoteEvent(NoteEventId.New(), pitch, 0, 0, 96));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoteEvent(NoteEventId.New(), pitch, 0, 480, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoteEvent(NoteEventId.New(), pitch, 0, 480, 128));
    }

    [Fact]
    public void NoteEventCommands_PreserveIdentityAndValuesAcrossUndoRedo()
    {
        var editor = new ProjectEditor(SongProject.Create("Playable notes"));
        var c4 = new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4);
        var d4 = new RegisteredPitch(NoteLetter.D, Accidental.Natural, 4);
        var add = new AddNoteEventCommand(c4, 0, 480, 96);

        editor.Execute(add);
        var id = Assert.Single(editor.Project.NoteEvents).Id;
        editor.Undo();
        Assert.Empty(editor.Project.NoteEvents);
        editor.Redo();
        Assert.Equal(id, Assert.Single(editor.Project.NoteEvents).Id);

        editor.Execute(new SetNoteEventCommand(id, d4, 240, 960, 80));
        var updated = Assert.Single(editor.Project.NoteEvents);
        Assert.Equal(id, updated.Id);
        Assert.Equal(62, updated.Pitch.MidiNumber);
        Assert.Equal(240, updated.StartTick);
        Assert.Equal(960, updated.DurationTicks);
        Assert.Equal(80, updated.Velocity);

        editor.Undo();
        var restored = Assert.Single(editor.Project.NoteEvents);
        Assert.Equal(id, restored.Id);
        Assert.Equal(60, restored.Pitch.MidiNumber);
        Assert.Equal(0, restored.StartTick);

        editor.Redo();
        Assert.Equal(62, Assert.Single(editor.Project.NoteEvents).Pitch.MidiNumber);

        editor.Execute(new RemoveNoteEventCommand(id));
        Assert.Empty(editor.Project.NoteEvents);
        editor.Undo();
        Assert.Equal(id, Assert.Single(editor.Project.NoteEvents).Id);
        editor.Redo();
        Assert.Empty(editor.Project.NoteEvents);
    }

    [Fact]
    public async Task SaveLoad_PreservesPlayableNoteEvents()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Playable notes");
            var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.B, Accidental.Flat, 3), 960, 240, 72);

            await repository.SaveAsync(project);
            var loaded = await repository.LoadAsync(project.Id);

            Assert.NotNull(loaded);
            var restored = Assert.Single(loaded.NoteEvents);
            Assert.Equal(note.Id, restored.Id);
            Assert.Equal("Bb3", restored.Pitch.ToDisplayString());
            Assert.Equal(960, restored.StartTick);
            Assert.Equal(240, restored.DurationTicks);
            Assert.Equal(72, restored.Velocity);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Schema17_MigratesWithoutInventingPlayableNotes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var project = SongProject.Create("Before playable notes");
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };
            var json = JsonNode.Parse(JsonSerializer.Serialize(project, options))!.AsObject();
            json["schemaVersion"] = 17;
            json.Remove("noteEvents");
            await File.WriteAllTextAsync(Path.Combine(directory, $"{project.Id}.json"), json.ToJsonString(options));

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(project.Id);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            Assert.Empty(loaded.NoteEvents);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
