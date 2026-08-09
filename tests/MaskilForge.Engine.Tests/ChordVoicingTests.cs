using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class ChordVoicingTests
{
    [Fact]
    public void SetChordVoicing_PreservesRegisteredVoicesAndStableIds()
    {
        var project = SongProject.Create("Voicing");
        var section = project.AddSection(SectionKind.Verse);
        var chord = project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0));
        var pitches = new[] { new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3), new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4) };

        var first = project.SetChordVoicing(section.Id, chord.Id, pitches, 36, 84).Voicing!;
        var second = project.SetChordVoicing(section.Id, chord.Id, pitches, 36, 84).Voicing!;

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Voices.Select(item => item.Id), second.Voices.Select(item => item.Id));
        Assert.Equal(new[] { 48, 55, 64 }, second.Voices.Select(item => item.Pitch.MidiNumber));
    }

    [Fact]
    public void SetChordVoicing_RejectsNotesOutsideChordOrRegister()
    {
        var project = SongProject.Create("Voicing");
        var section = project.AddSection(SectionKind.Verse);
        var chord = project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0));

        Assert.Throws<ArgumentException>(() => project.SetChordVoicing(section.Id, chord.Id, [new RegisteredPitch(NoteLetter.D, Accidental.Natural, 4)]));
        Assert.Throws<ArgumentException>(() => project.SetChordVoicing(section.Id, chord.Id, [new RegisteredPitch(NoteLetter.C, Accidental.Natural, 2)], 48, 84));
    }

    [Fact]
    public void SetChordVoicingCommand_UndoRedoRestoresExactIdentity()
    {
        var project = SongProject.Create("Voicing");
        var section = project.AddSection(SectionKind.Verse);
        var chord = project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0));
        var editor = new ProjectEditor(project);
        var command = new SetChordVoicingCommand(section.Id, chord.Id, [new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4)]);

        editor.Execute(command);
        var id = section.FindHarmonyChord(chord.Id).Voicing!.Id;
        editor.Undo();
        Assert.Null(section.FindHarmonyChord(chord.Id).Voicing);
        editor.Redo();
        Assert.Equal(id, section.FindHarmonyChord(chord.Id).Voicing!.Id);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesVoicingAndVoiceIdentities()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var project = SongProject.Create("Voicing");
            var section = project.AddSection(SectionKind.Verse);
            var chord = project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0));
            var voicing = project.SetChordVoicing(section.Id, chord.Id, [
                new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3),
                new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3),
                new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4)]).Voicing!;
            var repository = new JsonFileProjectRepository(directory);

            await repository.SaveAsync(project);
            var loaded = await repository.LoadAsync(project.Id);
            var restored = Assert.Single(Assert.Single(loaded!.Sections).Harmony).Voicing!;

            Assert.Equal(voicing.Id, restored.Id);
            Assert.Equal(voicing.Voices.Select(item => item.Id), restored.Voices.Select(item => item.Id));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
