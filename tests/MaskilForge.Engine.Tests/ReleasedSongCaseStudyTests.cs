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
    public void StructuralFunction_IsArtistAuthoredAndUndoableWithoutChangingSectionKind()
    {
        var project = SongProject.Create("Essence of Shadows");
        var chorus = project.AddSection(SectionKind.Chorus);
        var editor = new ProjectEditor(project);

        editor.Execute(new SetSectionStructuralFunctionCommand(chorus.Id, StructuralFunction.Payoff));

        Assert.Equal(SectionKind.Chorus, chorus.Kind);
        Assert.Equal(StructuralFunction.Payoff, chorus.StructuralFunction);
        editor.Undo();
        Assert.Equal(StructuralFunction.Unspecified, chorus.StructuralFunction);
        editor.Redo();
        Assert.Equal(StructuralFunction.Payoff, chorus.StructuralFunction);
    }

    [Fact]
    public void SectionIntent_SavesRoleAndPerformanceAsOneUndoableDecision()
    {
        var project = SongProject.Create("Essence of Shadows");
        var bridge = project.AddSection(SectionKind.Bridge);
        var editor = new ProjectEditor(project);

        editor.Execute(new SetSectionIntentCommand(
            bridge.Id,
            StructuralFunction.Contrast,
            SectionDelivery.Spoken,
            "Drop drums, near spoken"));

        Assert.Equal(StructuralFunction.Contrast, bridge.StructuralFunction);
        Assert.Equal(SectionDelivery.Spoken, bridge.Delivery);
        Assert.Equal("Drop drums, near spoken", bridge.PerformanceNotes);

        editor.Undo();
        Assert.Equal(StructuralFunction.Unspecified, bridge.StructuralFunction);
        Assert.Equal(SectionDelivery.Sung, bridge.Delivery);
        Assert.Empty(bridge.PerformanceNotes);

        editor.Redo();
        Assert.Equal(StructuralFunction.Contrast, bridge.StructuralFunction);
        Assert.Equal(SectionDelivery.Spoken, bridge.Delivery);
        Assert.Equal("Drop drums, near spoken", bridge.PerformanceNotes);
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
    public void ReuseSectionFoundation_ReplacesOnlyMusicalIntentWithFreshIdentitiesAndUndoRedo()
    {
        var project = SongProject.Create("Essence of Shadows");
        var source = project.AddSection(SectionKind.Chorus, "Chorus 1");
        var target = project.AddSection(SectionKind.Chorus, "Chorus 2");
        target.AddLyricLine("The second chorus keeps its own words");
        var sourceChord = project.AddHarmonyChord(source.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0), 2);
        project.SetChordVoicing(source.Id, sourceChord.Id, [new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4)]);
        project.SetSectionArrangement(source.Id, SectionEnergy.Strong, SectionDensity.Full);
        project.SetSectionRole(source.Id, ArrangementRole.HookReinforcement);
        project.AddHarmonyChord(target.Id, new ChordSymbol(NoteLetter.G), new BeatPosition(1, 1, 0));
        var previousChordId = target.Harmony[0].Id;
        var editor = new ProjectEditor(project);

        editor.Execute(new ReuseSectionFoundationCommand(source.Id, target.Id));

        Assert.Equal("The second chorus keeps its own words", Assert.Single(target.LyricLines).Text);
        Assert.Equal(sourceChord.Chord, Assert.Single(target.Harmony).Chord);
        Assert.NotEqual(sourceChord.Id, target.Harmony[0].Id);
        Assert.NotEqual(source.Harmony[0].Voicing!.Id, target.Harmony[0].Voicing!.Id);
        Assert.Equal(SectionEnergy.Strong, project.FindSectionArrangement(target.Id)!.Energy);
        Assert.Contains(project.ArrangementRoles, role => role.SectionId == target.Id && role.Role == ArrangementRole.HookReinforcement);

        var reusedChordId = target.Harmony[0].Id;
        editor.Undo();
        Assert.Equal(previousChordId, Assert.Single(target.Harmony).Id);
        Assert.Null(project.FindSectionArrangement(target.Id));
        Assert.DoesNotContain(project.ArrangementRoles, role => role.SectionId == target.Id);
        editor.Redo();
        Assert.Equal(reusedChordId, Assert.Single(target.Harmony).Id);
    }

    [Fact]
    public void ReuseSectionFoundation_RejectsTargetWithAbsoluteTimedPart()
    {
        var project = SongProject.Create("Essence of Shadows");
        var source = project.AddSection(SectionKind.Chorus, "Chorus 1");
        var target = project.AddSection(SectionKind.Chorus, "Chorus 2");
        var targetStartTick = project.Timeline.ToAbsoluteTicks(project.Timeline.FindSection(target.Id).Start);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), targetStartTick, 480, 96);
        project.SetSectionRole(target.Id, ArrangementRole.Foundation);
        project.AddMusicalPart(target.Id, ArrangementRole.Foundation, "Existing part", [note.Id]);

        var error = Assert.Throws<InvalidOperationException>(() => new ReuseSectionFoundationCommand(source.Id, target.Id).Execute(project));

        Assert.Contains("Remove this section's musical parts", error.Message);
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

    [Fact]
    public async Task SchemaV20_MigratesStructuralFunctionWithoutGenreOrSectionGuessing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var project = SongProject.Create("Migration");
        project.AddSection(SectionKind.Chorus);
        try
        {
            Directory.CreateDirectory(directory);
            var repository = new JsonFileProjectRepository(directory);
            await repository.SaveAsync(project, CancellationToken.None);
            var path = Path.Combine(directory, $"{project.Id}.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            json["schemaVersion"] = 20;
            json["sections"]![0]!.AsObject().Remove("structuralFunction");
            await File.WriteAllTextAsync(path, json.ToJsonString());

            var loaded = await repository.LoadAsync(project.Id);

            Assert.Equal(SchemaVersion.Current, loaded!.SchemaVersion);
            Assert.Equal(StructuralFunction.Unspecified, Assert.Single(loaded.Sections).StructuralFunction);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
