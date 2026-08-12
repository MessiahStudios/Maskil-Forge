using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class ReleasedSongCaseStudyTests
{
    [Fact]
    public void IntroAndPerformanceIntent_AreExplicitAndUndoable()
    {
        var project = SongProject.Create("Essence of Shadows");
        var intro = project.AddSection(SectionKind.Intro);
        var editor = new ProjectEditor(project);

        editor.Execute(new SetSectionPerformanceIntentCommand(
            intro.Id,
            SectionDelivery.Spoken,
            "Ambient piano + distant pad"));

        Assert.Equal("Intro", intro.Title);
        Assert.Equal(SectionDelivery.Spoken, intro.Delivery);
        Assert.Equal("Ambient piano + distant pad", intro.PerformanceNotes);
        editor.Undo();
        Assert.Equal(SectionDelivery.Sung, intro.Delivery);
        Assert.Empty(intro.PerformanceNotes);
        editor.Redo();
        Assert.Equal(SectionDelivery.Spoken, intro.Delivery);
    }

    [Fact]
    public void DuplicateSection_CopiesReusableIntentWithFreshIdentitiesAndStableUndoRedo()
    {
        var project = SongProject.Create("Essence of Shadows");
        var chorus = project.AddSection(SectionKind.Chorus);
        chorus.AddLyricLine("In the essence of shadows");
        chorus.SetPerformanceIntent(SectionDelivery.Sung, "Wider instrumentation, subtle harmony");
        project.SetSectionDuration(chorus.Id, 8);
        var chord = project.AddHarmonyChord(chorus.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0), 2);
        project.SetChordVoicing(chorus.Id, chord.Id, [new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4)]);
        project.SetSectionArrangement(chorus.Id, SectionEnergy.Strong, SectionDensity.Full);
        project.SetSectionRole(chorus.Id, ArrangementRole.HookReinforcement);
        var editor = new ProjectEditor(project);
        var command = new DuplicateSectionCommand(chorus.Id);

        editor.Execute(command);
        var duplicate = project.FindSection(command.DuplicateSectionId!.Value);

        Assert.Equal([chorus.Id, duplicate.Id], project.Sections.Select(section => section.Id));
        Assert.Equal("Chorus Copy", duplicate.Title);
        Assert.Equal(chorus.Delivery, duplicate.Delivery);
        Assert.Equal(chorus.PerformanceNotes, duplicate.PerformanceNotes);
        Assert.Equal(chorus.LyricLines.Select(line => line.Text), duplicate.LyricLines.Select(line => line.Text));
        Assert.NotEqual(chorus.LyricLines[0].Id, duplicate.LyricLines[0].Id);
        Assert.Equal(8, project.Timeline.FindSection(duplicate.Id).DurationBars);
        Assert.Equal(chord.Chord, Assert.Single(duplicate.Harmony).Chord);
        Assert.NotEqual(chord.Id, duplicate.Harmony[0].Id);
        Assert.Equal(chorus.Harmony[0].Voicing!.Voices.Select(voice => voice.Pitch), duplicate.Harmony[0].Voicing!.Voices.Select(voice => voice.Pitch));
        Assert.Equal(SectionEnergy.Strong, project.FindSectionArrangement(duplicate.Id)!.Energy);
        Assert.Contains(project.ArrangementRoles, role => role.SectionId == duplicate.Id && role.Role == ArrangementRole.HookReinforcement);

        var duplicateId = duplicate.Id;
        editor.Undo();
        Assert.Single(project.Sections);
        editor.Redo();
        Assert.Equal(duplicateId, project.Sections[1].Id);
    }

    [Fact]
    public void DuplicateSection_RejectsAbsoluteTimelineMaterial()
    {
        var project = SongProject.Create("Essence of Shadows");
        var chorus = project.AddSection(SectionKind.Chorus);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 96);
        project.SetSectionRole(chorus.Id, ArrangementRole.Foundation);
        project.AddMusicalPart(chorus.Id, ArrangementRole.Foundation, "Chorus foundation", [note.Id]);

        var error = Assert.Throws<InvalidOperationException>(() => new DuplicateSectionCommand(chorus.Id).Execute(project));

        Assert.Contains("timeline timing stays explicit", error.Message);
        Assert.Single(project.Sections);
    }

    [Fact]
    public async Task SchemaV19_MigratesSectionPerformanceIntentWithoutGuessing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, $"{projectId}.json"), $$"""
            {
              "id": "{{projectId}}", "schemaVersion": 19, "title": "Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{ "sectionId": "{{sectionId}}", "start": { "bar": 1, "beat": 1, "tick": 0 }, "durationBars": 4 }]
              },
              "sections": [{
                "id": "{{sectionId}}", "kind": "Verse", "title": "Verse",
                "lyricLines": [], "harmony": [], "harmonyCandidates": []
              }],
              "tracks": [], "locks": [], "arrangement": [], "arrangementRoles": [],
              "noteEvents": [], "musicalParts": [],
              "key": { "tonic": "C", "accidental": "Natural", "mode": "Major" }
            }
            """);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId);

            Assert.Equal(SchemaVersion.Current, loaded!.SchemaVersion);
            var migrated = Assert.Single(loaded.Sections);
            Assert.Equal(SectionDelivery.Sung, migrated.Delivery);
            Assert.Empty(migrated.PerformanceNotes);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
