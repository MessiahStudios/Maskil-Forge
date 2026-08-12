using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;
using System.Text.Json;

namespace MaskilForge.Engine.Tests;

public sealed class JsonPersistenceTests
{
    [Fact]
    public async Task SaveAndLoad_PreservesIdentifiersOrderingAndLyrics()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MaskilForge.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var savingRepository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Round Trip");
            project.SetArtist("Maskil Artist");
            project.SetGenre(SongGenre.Alternative);
            project.SetDescription("A persistence proof.");
            project.SetRawLyricDraft("Unstructured source words that must remain intact.");
            project.SetTempo(84);
            project.SetTimeSignature(6, 8);
            var verse = project.AddSection(SectionKind.Verse);
            var verseLine = verse.AddLyricLine("I walked, through shadows");
            verseLine.SetSyllables(verseLine.Words[2].Id, ["through"]);
            verseLine.SetStress(
                verseLine.Words[2].Id,
                verseLine.Words[2].Syllables[0].Id,
                StressLevel.Primary);
            verseLine.SplitPhraseAfter(verseLine.Words[1].Id);
            verseLine.SetProsodicWeight(
                verseLine.Phrases[1].Id,
                verseLine.Words[2].Syllables[0].Id,
                ProsodicWeight.Strong);
            project.SetSyllablePlacement(
                verse.Id,
                verseLine.Id,
                verseLine.Words[2].Syllables[0].Id,
                new BeatPosition(2, 3, 120));
            var rhythmCandidate = project.CaptureRhythmCandidate(
                verse.Id,
                verseLine.Id,
                verseLine.Phrases[1].Id,
                "Verse push");
            verseLine.SetBreathPoint(verseLine.Words[2].Syllables[0].Id, true);
            var verseWordIds = verseLine.Words.Select(word => word.Id).ToList();
            var syllableId = verseLine.Words[2].Syllables[0].Id;
            var punctuationId = verseLine.Punctuation[0].Id;
            var phraseIds = verseLine.Phrases.Select(item => item.Id).ToList();
            var prosodicPatternId = verseLine.Phrases[1].Prosody!.Id;
            var prosodicUnitId = verseLine.Phrases[1].Prosody!.Units[0].Id;
            var syllablePlacementId = verseLine.SyllablePlacements[0].Id;
            var rhythmCandidateId = rhythmCandidate.Id;
            var rhythmEventId = rhythmCandidate.Events[0].Id;
            var breathPointId = verseLine.BreathPoints[0].Id;
            var chorus = project.AddSection(SectionKind.Chorus);
            var chorusLine = chorus.AddLyricLine("You brought me home");
            project.SetSectionDuration(verse.Id, 12);

            await savingRepository.SaveAsync(project, CancellationToken.None);

            // A new repository instance represents closing the application and reloading from disk.
            var loadingRepository = new JsonFileProjectRepository(directory);
            var loaded = await loadingRepository.LoadAsync(project.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(project.Id, loaded.Id);
            Assert.Equal([verse.Id, chorus.Id], loaded.Sections.Select(section => section.Id));
            Assert.Equal(verseLine.Id, loaded.Sections[0].LyricLines[0].Id);
            Assert.Equal("I walked, through shadows", loaded.Sections[0].LyricLines[0].Text);
            Assert.Equal(verseWordIds, loaded.Sections[0].LyricLines[0].Words.Select(word => word.Id));
            Assert.Equal(syllableId, loaded.Sections[0].LyricLines[0].Words[2].Syllables[0].Id);
            Assert.Equal(0, loaded.Sections[0].LyricLines[0].Words[2].Syllables[0].Position);
            Assert.Equal(SyllableSource.Manual, loaded.Sections[0].LyricLines[0].Words[2].Syllables[0].Source);
            Assert.Equal(StressLevel.Primary, loaded.Sections[0].LyricLines[0].Words[2].Syllables[0].Stress?.Level);
            Assert.Equal(StressProvenance.Manual, loaded.Sections[0].LyricLines[0].Words[2].Syllables[0].Stress?.Provenance);
            Assert.Equal(punctuationId, loaded.Sections[0].LyricLines[0].Punctuation[0].Id);
            Assert.Equal(phraseIds, loaded.Sections[0].LyricLines[0].Phrases.Select(item => item.Id));
            Assert.All(loaded.Sections[0].LyricLines[0].Phrases, item => Assert.Equal(PhraseSource.Manual, item.Source));
            var loadedProsody = Assert.IsType<ProsodicPattern>(loaded.Sections[0].LyricLines[0].Phrases[1].Prosody);
            var loadedUnit = Assert.Single(loadedProsody.Units);
            Assert.Equal(prosodicPatternId, loadedProsody.Id);
            Assert.Equal(prosodicUnitId, loadedUnit.Id);
            Assert.Equal(syllableId, loadedUnit.SyllableId);
            Assert.Equal(ProsodicWeight.Strong, loadedUnit.Weight);
            Assert.Equal(ProsodyProvenance.Manual, loadedUnit.Provenance);
            var loadedPlacement = Assert.Single(loaded.Sections[0].LyricLines[0].SyllablePlacements);
            Assert.Equal(syllablePlacementId, loadedPlacement.Id);
            Assert.Equal(syllableId, loadedPlacement.SyllableId);
            Assert.Equal(new BeatPosition(2, 3, 120), loadedPlacement.Position);
            Assert.Equal(PlacementProvenance.Manual, loadedPlacement.Provenance);
            var loadedCandidate = Assert.Single(loaded.Sections[0].LyricLines[0].RhythmCandidates);
            var loadedRhythmEvent = Assert.Single(loadedCandidate.Events);
            Assert.Equal(rhythmCandidateId, loadedCandidate.Id);
            Assert.Equal("Verse push", loadedCandidate.Label);
            Assert.Equal(phraseIds[1], loadedCandidate.PhraseId);
            Assert.Equal(RhythmCandidateProvenance.Manual, loadedCandidate.Provenance);
            Assert.Equal(rhythmEventId, loadedRhythmEvent.Id);
            Assert.Equal(syllableId, loadedRhythmEvent.SyllableId);
            Assert.Equal(new BeatPosition(2, 3, 120), loadedRhythmEvent.BeatPosition);
            var loadedBreath = Assert.Single(loaded.Sections[0].LyricLines[0].BreathPoints);
            Assert.Equal(breathPointId, loadedBreath.Id);
            Assert.Equal(syllableId, loadedBreath.AfterSyllableId);
            Assert.Equal(BreathProvenance.Manual, loadedBreath.Provenance);
            Assert.Equal(chorusLine.Id, loaded.Sections[1].LyricLines[0].Id);
            Assert.Equal("You brought me home", loaded.Sections[1].LyricLines[0].Text);
            Assert.Equal("Maskil Artist", loaded.Artist);
            Assert.Equal(SongGenre.Alternative, loaded.Genre);
            Assert.Equal("A persistence proof.", loaded.Description);
            Assert.Equal("Unstructured source words that must remain intact.", loaded.RawLyricDraft);
            Assert.Equal(84, loaded.Tempo.BeatsPerMinute);
            Assert.Equal((6, 8), (loaded.TimeSignature.Numerator, loaded.TimeSignature.Denominator));
            Assert.Equal([verse.Id, chorus.Id], loaded.Timeline.SectionPlacements.Select(item => item.SectionId));
            Assert.Equal([1, 13], loaded.Timeline.SectionPlacements.Select(item => item.Start.Bar));
            Assert.Equal([12, 8], loaded.Timeline.SectionPlacements.Select(item => item.DurationBars));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ListAsync_ReturnsProjectSummariesWithoutRequiringKnownIds()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var draft = SongProject.Create("Raw Draft");
            draft.SetRawLyricDraft("A loose lyric idea");
            var structured = SongProject.Create("Structured Song");
            structured.AddSection(SectionKind.Verse);
            await repository.SaveAsync(draft, CancellationToken.None);
            await repository.SaveAsync(structured, CancellationToken.None);

            var summaries = await repository.ListAsync(CancellationToken.None);

            Assert.Equal(2, summaries.Count);
            Assert.Contains(summaries, item => item.Id == draft.Id && item.HasRawLyrics && item.SectionCount == 0);
            Assert.Contains(summaries, item => item.Id == structured.Id && !item.HasRawLyrics && item.SectionCount == 1);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task MoveToTrash_RemovesProjectFromLibraryWithoutDestroyingJson()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Recoverable Song");
            var section = project.AddSection(SectionKind.Verse);
            var lyric = section.AddLyricLine("Identity must survive recovery");
            await repository.SaveAsync(project, CancellationToken.None);

            Assert.True(await repository.MoveToTrashAsync(project.Id, CancellationToken.None));
            Assert.Null(await repository.LoadAsync(project.Id, CancellationToken.None));
            Assert.Empty(await repository.ListAsync(CancellationToken.None));
            Assert.Single(Directory.EnumerateFiles(Path.Combine(directory, "trash"), "*.json"));

            var trash = await repository.ListTrashAsync(CancellationToken.None);
            Assert.Single(trash);
            Assert.Equal(project.Id, trash[0].Id);

            Assert.True(await repository.RestoreFromTrashAsync(project.Id, CancellationToken.None));
            var restored = await repository.LoadAsync(project.Id, CancellationToken.None);
            Assert.NotNull(restored);
            Assert.Equal(project.Id, restored.Id);
            Assert.Equal(section.Id, restored.Sections[0].Id);
            Assert.Equal(lyric.Id, restored.Sections[0].LyricLines[0].Id);
            Assert.Empty(await repository.ListTrashAsync(CancellationToken.None));

            Assert.True(await repository.MoveToTrashAsync(project.Id, CancellationToken.None));
            Assert.True(await repository.PermanentlyDeleteAsync(project.Id, CancellationToken.None));
            Assert.Empty(await repository.ListTrashAsync(CancellationToken.None));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(directory, "trash"), "*.json"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task SchemaV20_WritesStableSongGraphAndSectionPerformanceIntentContract()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Schema Contract");
            var section = project.AddSection(SectionKind.Verse);
            var line = section.AddLyricLine("Words become addressable!");
            line.SetSyllables(line.Words[1].Id, ["be", "come"]);
            line.SetStress(line.Words[1].Id, line.Words[1].Syllables[1].Id, StressLevel.Emphasized);
            line.SplitPhraseAfter(line.Words[1].Id);
            line.SetProsodicWeight(
                line.Phrases[0].Id,
                line.Words[1].Syllables[1].Id,
                ProsodicWeight.Strong);
            project.SetSyllablePlacement(
                project.Sections[0].Id,
                line.Id,
                line.Words[1].Syllables[1].Id,
                new BeatPosition(2, 3, 120));
            project.CaptureRhythmCandidate(
                project.Sections[0].Id,
                line.Id,
                line.Phrases[0].Id,
                "Option A");
            line.SetBreathPoint(line.Words[1].Syllables[1].Id, true);
            var lineLock = project.LockLyricLine(line.Id);
            project.AddHarmonyChord(
                project.Sections[0].Id,
                new ChordSymbol(NoteLetter.G, Accidental.Natural, ChordQuality.DominantSeventh),
                new BeatPosition(1, 1, 0),
                2);
            var arrangement = project.SetSectionArrangement(section.Id, SectionEnergy.Building, SectionDensity.Light);
            var arrangementRole = project.SetSectionRole(section.Id, ArrangementRole.Pulse);
            var noteEvent = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 96);
            var musicalPart = project.AddMusicalPart(section.Id, ArrangementRole.Pulse, "Verse pulse", [noteEvent.Id]);
            await repository.SaveAsync(project, CancellationToken.None);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, $"{project.Id}.json")));
            var root = document.RootElement;
            Assert.Equal(20, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(project.Id.ToString(), root.GetProperty("id").GetString());
            Assert.Equal("Schema Contract", root.GetProperty("title").GetString());
            Assert.Equal(JsonValueKind.String, root.GetProperty("createdUtc").ValueKind);
            Assert.Equal(JsonValueKind.String, root.GetProperty("lastModifiedUtc").ValueKind);
            Assert.Equal(JsonValueKind.Array, root.GetProperty("sections").ValueKind);
            var serializedSection = Assert.Single(root.GetProperty("sections").EnumerateArray());
            Assert.Equal("Sung", serializedSection.GetProperty("delivery").GetString());
            Assert.Equal(string.Empty, serializedSection.GetProperty("performanceNotes").GetString());
            Assert.Equal(JsonValueKind.Array, root.GetProperty("tracks").ValueKind);
            Assert.False(root.TryGetProperty("tempo", out _));
            Assert.False(root.TryGetProperty("timeSignature", out _));
            var timeline = root.GetProperty("timeline");
            Assert.Equal(480, timeline.GetProperty("ticksPerQuarterNote").GetInt32());
            Assert.Single(timeline.GetProperty("tempoMap").GetProperty("events").EnumerateArray());
            Assert.Single(timeline.GetProperty("timeSignatureMap").GetProperty("events").EnumerateArray());
            Assert.Single(timeline.GetProperty("sectionPlacements").EnumerateArray());
            var savedLine = root.GetProperty("sections")[0].GetProperty("lyricLines")[0];
            Assert.Equal(3, savedLine.GetProperty("words").GetArrayLength());
            Assert.All(savedLine.GetProperty("words").EnumerateArray(), word =>
            {
                Assert.Equal(JsonValueKind.String, word.GetProperty("id").ValueKind);
                Assert.Equal(JsonValueKind.Array, word.GetProperty("syllables").ValueKind);
            });
            var syllables = savedLine.GetProperty("words")[1].GetProperty("syllables");
            Assert.Equal([0, 1], syllables.EnumerateArray().Select(item => item.GetProperty("position").GetInt32()));
            Assert.All(syllables.EnumerateArray(), item => Assert.Equal("Manual", item.GetProperty("source").GetString()));
            Assert.Equal(JsonValueKind.Null, syllables[0].GetProperty("stress").ValueKind);
            Assert.Equal("Emphasized", syllables[1].GetProperty("stress").GetProperty("level").GetString());
            Assert.Equal("Manual", syllables[1].GetProperty("stress").GetProperty("provenance").GetString());
            var punctuation = Assert.Single(savedLine.GetProperty("punctuation").EnumerateArray());
            Assert.Equal("!", punctuation.GetProperty("text").GetString());
            Assert.Equal(2, savedLine.GetProperty("phrases").GetArrayLength());
            Assert.All(savedLine.GetProperty("phrases").EnumerateArray(), phrase =>
            {
                Assert.Equal(JsonValueKind.String, phrase.GetProperty("id").ValueKind);
                Assert.Equal("Manual", phrase.GetProperty("source").GetString());
                Assert.Equal(JsonValueKind.Array, phrase.GetProperty("wordIds").ValueKind);
            });
            var prosody = savedLine.GetProperty("phrases")[0].GetProperty("prosody");
            Assert.Equal(JsonValueKind.String, prosody.GetProperty("id").ValueKind);
            var unit = Assert.Single(prosody.GetProperty("units").EnumerateArray());
            Assert.Equal(JsonValueKind.String, unit.GetProperty("id").ValueKind);
            Assert.Equal(line.Words[1].Syllables[1].Id.ToString(), unit.GetProperty("syllableId").GetString());
            Assert.Equal(0, unit.GetProperty("position").GetInt32());
            Assert.Equal("Strong", unit.GetProperty("weight").GetString());
            Assert.Equal("Manual", unit.GetProperty("provenance").GetString());
            Assert.Equal(JsonValueKind.Null, savedLine.GetProperty("phrases")[1].GetProperty("prosody").ValueKind);
            var placement = Assert.Single(savedLine.GetProperty("syllablePlacements").EnumerateArray());
            Assert.Equal(JsonValueKind.String, placement.GetProperty("id").ValueKind);
            Assert.Equal(line.Words[1].Syllables[1].Id.ToString(), placement.GetProperty("syllableId").GetString());
            Assert.Equal(2, placement.GetProperty("position").GetProperty("bar").GetInt32());
            Assert.Equal(3, placement.GetProperty("position").GetProperty("beat").GetInt32());
            Assert.Equal(120, placement.GetProperty("position").GetProperty("tick").GetInt32());
            Assert.Equal("Manual", placement.GetProperty("provenance").GetString());
            var candidate = Assert.Single(savedLine.GetProperty("rhythmCandidates").EnumerateArray());
            Assert.Equal(JsonValueKind.String, candidate.GetProperty("id").ValueKind);
            Assert.Equal(line.Phrases[0].Id.ToString(), candidate.GetProperty("phraseId").GetString());
            Assert.Equal("Option A", candidate.GetProperty("label").GetString());
            Assert.Equal("Manual", candidate.GetProperty("provenance").GetString());
            var rhythmEvent = Assert.Single(candidate.GetProperty("events").EnumerateArray());
            Assert.Equal(JsonValueKind.String, rhythmEvent.GetProperty("id").ValueKind);
            Assert.Equal(line.Words[1].Syllables[1].Id.ToString(), rhythmEvent.GetProperty("syllableId").GetString());
            Assert.Equal(0, rhythmEvent.GetProperty("position").GetInt32());
            Assert.Equal(2, rhythmEvent.GetProperty("beatPosition").GetProperty("bar").GetInt32());
            var breath = Assert.Single(savedLine.GetProperty("breathPoints").EnumerateArray());
            Assert.Equal(JsonValueKind.String, breath.GetProperty("id").ValueKind);
            Assert.Equal(line.Words[1].Syllables[1].Id.ToString(), breath.GetProperty("afterSyllableId").GetString());
            Assert.Equal("Manual", breath.GetProperty("provenance").GetString());
            var creativeLock = Assert.Single(root.GetProperty("locks").EnumerateArray());
            Assert.Equal(lineLock.Id.ToString(), creativeLock.GetProperty("id").GetString());
            Assert.Equal("LyricLine", creativeLock.GetProperty("scope").GetString());
            Assert.Equal(line.Id.ToString(), creativeLock.GetProperty("lineId").GetString());
            Assert.Equal(JsonValueKind.Null, creativeLock.GetProperty("phraseId").ValueKind);
            Assert.Equal("Manual", creativeLock.GetProperty("provenance").GetString());
            var key = root.GetProperty("key");
            Assert.Equal("C", key.GetProperty("tonic").GetString());
            Assert.Equal("Natural", key.GetProperty("accidental").GetString());
            Assert.Equal("Major", key.GetProperty("mode").GetString());
            var harmony = Assert.Single(root.GetProperty("sections")[0].GetProperty("harmony").EnumerateArray());
            Assert.Equal(JsonValueKind.String, harmony.GetProperty("id").ValueKind);
            Assert.Equal("G", harmony.GetProperty("chord").GetProperty("root").GetString());
            Assert.Equal("DominantSeventh", harmony.GetProperty("chord").GetProperty("quality").GetString());
            Assert.Equal(1, harmony.GetProperty("start").GetProperty("bar").GetInt32());
            Assert.Equal(2, harmony.GetProperty("durationBars").GetInt32());
            Assert.Equal("Manual", harmony.GetProperty("provenance").GetString());
            Assert.Equal(JsonValueKind.Null, harmony.GetProperty("voicing").ValueKind);
            var savedArrangement = Assert.Single(root.GetProperty("arrangement").EnumerateArray());
            Assert.Equal(arrangement.Id.ToString(), savedArrangement.GetProperty("id").GetString());
            Assert.Equal(section.Id.ToString(), savedArrangement.GetProperty("sectionId").GetString());
            Assert.Equal("Building", savedArrangement.GetProperty("energy").GetString());
            Assert.Equal("Light", savedArrangement.GetProperty("density").GetString());
            Assert.Equal("Manual", savedArrangement.GetProperty("provenance").GetString());
            var savedRole = Assert.Single(root.GetProperty("arrangementRoles").EnumerateArray());
            Assert.Equal(arrangementRole.Id.ToString(), savedRole.GetProperty("id").GetString());
            Assert.Equal(section.Id.ToString(), savedRole.GetProperty("sectionId").GetString());
            Assert.Equal("Pulse", savedRole.GetProperty("role").GetString());
            Assert.Equal("Manual", savedRole.GetProperty("provenance").GetString());
            var savedNote = Assert.Single(root.GetProperty("noteEvents").EnumerateArray());
            Assert.Equal(noteEvent.Id.ToString(), savedNote.GetProperty("id").GetString());
            Assert.Equal("C", savedNote.GetProperty("pitch").GetProperty("letter").GetString());
            Assert.Equal(4, savedNote.GetProperty("pitch").GetProperty("octave").GetInt32());
            Assert.Equal(0, savedNote.GetProperty("startTick").GetInt64());
            Assert.Equal(480, savedNote.GetProperty("durationTicks").GetInt64());
            Assert.Equal(96, savedNote.GetProperty("velocity").GetInt32());
            var savedPart = Assert.Single(root.GetProperty("musicalParts").EnumerateArray());
            Assert.Equal(musicalPart.Id.ToString(), savedPart.GetProperty("id").GetString());
            Assert.Equal(section.Id.ToString(), savedPart.GetProperty("sectionId").GetString());
            Assert.Equal("Pulse", savedPart.GetProperty("role").GetString());
            Assert.Equal("Verse pulse", savedPart.GetProperty("label").GetString());
            Assert.Equal(noteEvent.Id.ToString(), Assert.Single(savedPart.GetProperty("noteEventIds").EnumerateArray()).GetString());
            Assert.Equal("Manual", savedPart.GetProperty("provenance").GetString());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_ReadsOriginalSchemaV1FilesMissingNewFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var id = ProjectId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV1 = $$"""
            {
              "id": "{{id}}",
              "schemaVersion": { "value": 1 },
              "title": "Original V1 Song",
              "artist": "",
              "genre": "Unspecified",
              "description": "",
              "tempo": { "beat": 0, "beatsPerMinute": 120 },
              "timeSignature": { "beat": 0, "numerator": 4, "denominator": 4 }
            }
            """;
            await File.WriteAllTextAsync(Path.Combine(directory, $"{id}.json"), originalV1);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(id, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(id, loaded.Id);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            Assert.Equal("Original V1 Song", loaded.Title);
            Assert.Equal(string.Empty, loaded.RawLyricDraft);
            Assert.Empty(loaded.Sections);
            Assert.Empty(loaded.Tracks);
            Assert.NotEqual(default, loaded.CreatedUtc);
            Assert.Equal(loaded.CreatedUtc, loaded.LastModifiedUtc);
            Assert.Equal(480, loaded.Timeline.TicksPerQuarterNote);
            Assert.Equal(120, loaded.Timeline.TempoMap.Events[0].BeatsPerMinute);
            Assert.Equal(4, loaded.Timeline.TimeSignatureMap.Events[0].Numerator);
            Assert.Equal(4, loaded.Timeline.TimeSignatureMap.Events[0].Denominator);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV1SectionsToOrderedTimelinePlacements()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var verseId = SectionId.New();
        var chorusId = SectionId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV1 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 1,
              "title": "Migration Song",
              "tempo": { "beat": 0, "beatsPerMinute": 96 },
              "timeSignature": { "beat": 0, "numerator": 6, "denominator": 8 },
              "sections": [
                { "id": "{{verseId}}", "kind": "Verse", "title": "Verse", "lyricLines": [] },
                { "id": "{{chorusId}}", "kind": "Chorus", "title": "Chorus", "lyricLines": [] }
              ]
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV1);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            Assert.Equal(96, loaded.Timeline.TempoMap.Events[0].BeatsPerMinute);
            Assert.Equal(6, loaded.Timeline.TimeSignatureMap.Events[0].Numerator);
            Assert.Equal(8, loaded.Timeline.TimeSignatureMap.Events[0].Denominator);
            Assert.Equal([verseId, chorusId], loaded.Timeline.SectionPlacements.Select(item => item.SectionId));
            Assert.Equal([1, 9], loaded.Timeline.SectionPlacements.Select(item => item.Start.Bar));
            Assert.All(loaded.Timeline.SectionPlacements, item => Assert.Equal(8, item.DurationBars));
            Assert.Equal(originalV1, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV2WordsWithDeterministicIdentifiers()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        var lineId = LyricLineId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV2 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 2,
              "title": "Lyric Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{
                  "sectionId": "{{sectionId}}",
                  "start": { "bar": 1, "beat": 1, "tick": 0 },
                  "durationBars": 8
                }]
              },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": [{ "id": "{{lineId}}", "text": "Amazing grace, how sweet" }]
              }],
              "tracks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV2);

            var first = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);
            var second = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(SchemaVersion.Current, first.SchemaVersion);
            Assert.Equal(["Amazing", "grace", "how", "sweet"], first.Sections[0].LyricLines[0].Words.Select(word => word.Text));
            Assert.Equal(
                first.Sections[0].LyricLines[0].Words.Select(word => word.Id),
                second.Sections[0].LyricLines[0].Words.Select(word => word.Id));
            Assert.All(first.Sections[0].LyricLines[0].Words, word => Assert.Empty(word.Syllables));
            Assert.Equal(originalV2, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV3SyllablesToOrderedManualProvenance()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        var lineId = LyricLineId.New();
        var wordId = LyricWordId.New();
        var firstSyllableId = SyllableId.New();
        var secondSyllableId = SyllableId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV3 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 3,
              "title": "Syllable Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{
                  "sectionId": "{{sectionId}}",
                  "start": { "bar": 1, "beat": 1, "tick": 0 },
                  "durationBars": 8
                }]
              },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": [{
                  "id": "{{lineId}}",
                  "text": "heaven",
                  "words": [{
                    "id": "{{wordId}}",
                    "text": "heaven",
                    "start": 0,
                    "length": 6,
                    "syllables": [
                      { "id": "{{firstSyllableId}}", "text": "heav" },
                      { "id": "{{secondSyllableId}}", "text": "en" }
                    ]
                  }]
                }]
              }],
              "tracks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV3);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            var syllables = loaded.Sections[0].LyricLines[0].Words[0].Syllables;
            Assert.Equal([firstSyllableId, secondSyllableId], syllables.Select(item => item.Id));
            Assert.Equal([0, 1], syllables.Select(item => item.Position));
            Assert.All(syllables, item => Assert.Equal(SyllableSource.Manual, item.Source));
            Assert.Equal(originalV3, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV4ToDeterministicPunctuationAndDefaultPhrase()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        var lineId = LyricLineId.New();
        var heavenId = LyricWordId.New();
        var nowId = LyricWordId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV4 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 4,
              "title": "Phrase Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{
                  "sectionId": "{{sectionId}}",
                  "start": { "bar": 1, "beat": 1, "tick": 0 },
                  "durationBars": 8
                }]
              },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": [{
                  "id": "{{lineId}}",
                  "text": "heaven, now",
                  "words": [
                    { "id": "{{heavenId}}", "text": "heaven", "start": 0, "length": 6, "syllables": [] },
                    { "id": "{{nowId}}", "text": "now", "start": 8, "length": 3, "syllables": [] }
                  ]
                }]
              }],
              "tracks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV4);

            var first = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);
            var second = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(first);
            Assert.NotNull(second);
            var line = first.Sections[0].LyricLines[0];
            var repeatedLine = second.Sections[0].LyricLines[0];
            var punctuation = Assert.Single(line.Punctuation);
            Assert.Equal(",", punctuation.Text);
            Assert.Equal(6, punctuation.Start);
            Assert.Equal(punctuation.Id, Assert.Single(repeatedLine.Punctuation).Id);
            var phrase = Assert.Single(line.Phrases);
            Assert.Equal(PhraseSource.Default, phrase.Source);
            Assert.Equal([heavenId, nowId], phrase.WordIds);
            Assert.Equal(phrase.Id, Assert.Single(repeatedLine.Phrases).Id);
            Assert.Equal(originalV4, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV5SyllablesToUnmarkedStress()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        var lineId = LyricLineId.New();
        var wordId = LyricWordId.New();
        var syllableId = SyllableId.New();
        var phraseId = LyricPhraseId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV5 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 5,
              "title": "Stress Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{
                  "sectionId": "{{sectionId}}",
                  "start": { "bar": 1, "beat": 1, "tick": 0 },
                  "durationBars": 8
                }]
              },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": [{
                  "id": "{{lineId}}",
                  "text": "pain",
                  "words": [{
                    "id": "{{wordId}}",
                    "text": "pain",
                    "start": 0,
                    "length": 4,
                    "syllables": [{
                      "id": "{{syllableId}}",
                      "text": "pain",
                      "position": 0,
                      "source": "Manual"
                    }]
                  }],
                  "punctuation": [],
                  "phrases": [{
                    "id": "{{phraseId}}",
                    "position": 0,
                    "wordIds": ["{{wordId}}"],
                    "source": "Default"
                  }]
                }]
              }],
              "tracks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV5);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            var syllable = Assert.Single(loaded.Sections[0].LyricLines[0].Words[0].Syllables);
            Assert.Equal(syllableId, syllable.Id);
            Assert.Null(syllable.Stress);
            Assert.Equal(originalV5, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV6PhrasesToUndecidedProsody()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        var lineId = LyricLineId.New();
        var wordId = LyricWordId.New();
        var syllableId = SyllableId.New();
        var phraseId = LyricPhraseId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV6 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 6,
              "title": "Prosody Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{
                  "sectionId": "{{sectionId}}",
                  "start": { "bar": 1, "beat": 1, "tick": 0 },
                  "durationBars": 8
                }]
              },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": [{
                  "id": "{{lineId}}",
                  "text": "pain",
                  "words": [{
                    "id": "{{wordId}}",
                    "text": "pain",
                    "start": 0,
                    "length": 4,
                    "syllables": [{
                      "id": "{{syllableId}}",
                      "text": "pain",
                      "position": 0,
                      "source": "Manual",
                      "stress": { "level": "Primary", "provenance": "Manual" }
                    }]
                  }],
                  "punctuation": [],
                  "phrases": [{
                    "id": "{{phraseId}}",
                    "position": 0,
                    "wordIds": ["{{wordId}}"],
                    "source": "Manual"
                  }]
                }]
              }],
              "tracks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV6);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            var line = loaded.Sections[0].LyricLines[0];
            var syllable = Assert.Single(line.Words[0].Syllables);
            Assert.Equal(syllableId, syllable.Id);
            Assert.Equal(StressLevel.Primary, syllable.Stress?.Level);
            var phrase = Assert.Single(line.Phrases);
            Assert.Equal(phraseId, phrase.Id);
            Assert.Null(phrase.Prosody);
            Assert.Equal(originalV6, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV7LinesToUnplacedSyllables()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        var lineId = LyricLineId.New();
        var wordId = LyricWordId.New();
        var syllableId = SyllableId.New();
        var phraseId = LyricPhraseId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV7 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 7,
              "title": "Beat Mapping Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{
                  "sectionId": "{{sectionId}}",
                  "start": { "bar": 1, "beat": 1, "tick": 0 },
                  "durationBars": 8
                }]
              },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": [{
                  "id": "{{lineId}}",
                  "text": "pain",
                  "words": [{
                    "id": "{{wordId}}",
                    "text": "pain",
                    "start": 0,
                    "length": 4,
                    "syllables": [{
                      "id": "{{syllableId}}",
                      "text": "pain",
                      "position": 0,
                      "source": "Manual",
                      "stress": { "level": "Primary", "provenance": "Manual" }
                    }]
                  }],
                  "punctuation": [],
                  "phrases": [{
                    "id": "{{phraseId}}",
                    "position": 0,
                    "wordIds": ["{{wordId}}"],
                    "source": "Manual",
                    "prosody": null
                  }]
                }]
              }],
              "tracks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV7);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            var line = loaded.Sections[0].LyricLines[0];
            Assert.Equal(syllableId, Assert.Single(line.Words[0].Syllables).Id);
            Assert.Empty(line.SyllablePlacements);
            Assert.Equal(originalV7, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV8WithoutInventingRhythmCandidates()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        var lineId = LyricLineId.New();
        var wordId = LyricWordId.New();
        var syllableId = SyllableId.New();
        var phraseId = LyricPhraseId.New();
        var placementId = SyllablePlacementId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV8 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 8,
              "title": "Rhythm Candidate Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{
                  "sectionId": "{{sectionId}}",
                  "start": { "bar": 1, "beat": 1, "tick": 0 },
                  "durationBars": 8
                }]
              },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": [{
                  "id": "{{lineId}}",
                  "text": "pain",
                  "words": [{
                    "id": "{{wordId}}",
                    "text": "pain",
                    "start": 0,
                    "length": 4,
                    "syllables": [{
                      "id": "{{syllableId}}",
                      "text": "pain",
                      "position": 0,
                      "source": "Manual",
                      "stress": null
                    }]
                  }],
                  "punctuation": [],
                  "phrases": [{
                    "id": "{{phraseId}}",
                    "position": 0,
                    "wordIds": ["{{wordId}}"],
                    "source": "Manual",
                    "prosody": null
                  }],
                  "syllablePlacements": [{
                    "id": "{{placementId}}",
                    "syllableId": "{{syllableId}}",
                    "position": { "bar": 2, "beat": 3, "tick": 120 },
                    "provenance": "Manual"
                  }]
                }]
              }],
              "tracks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV8);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            var line = loaded.Sections[0].LyricLines[0];
            Assert.Equal(placementId, Assert.Single(line.SyllablePlacements).Id);
            Assert.Empty(line.RhythmCandidates);
            Assert.Empty(line.BreathPoints);
            Assert.Equal(originalV8, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV9WithoutInventingBreathPoints()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        var lineId = LyricLineId.New();
        var wordId = LyricWordId.New();
        var syllableId = SyllableId.New();
        var phraseId = LyricPhraseId.New();
        var placementId = SyllablePlacementId.New();
        var candidateId = RhythmCandidateId.New();
        var eventId = RhythmCandidateEventId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV9 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 9,
              "title": "Breath Point Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{
                  "sectionId": "{{sectionId}}",
                  "start": { "bar": 1, "beat": 1, "tick": 0 },
                  "durationBars": 8
                }]
              },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": [{
                  "id": "{{lineId}}",
                  "text": "pain",
                  "words": [{
                    "id": "{{wordId}}",
                    "text": "pain",
                    "start": 0,
                    "length": 4,
                    "syllables": [{
                      "id": "{{syllableId}}",
                      "text": "pain",
                      "position": 0,
                      "source": "Manual",
                      "stress": null
                    }]
                  }],
                  "punctuation": [],
                  "phrases": [{
                    "id": "{{phraseId}}",
                    "position": 0,
                    "wordIds": ["{{wordId}}"],
                    "source": "Manual",
                    "prosody": null
                  }],
                  "syllablePlacements": [{
                    "id": "{{placementId}}",
                    "syllableId": "{{syllableId}}",
                    "position": { "bar": 2, "beat": 3, "tick": 120 },
                    "provenance": "Manual"
                  }],
                  "rhythmCandidates": [{
                    "id": "{{candidateId}}",
                    "phraseId": "{{phraseId}}",
                    "label": "Option A",
                    "provenance": "Manual",
                    "events": [{
                      "id": "{{eventId}}",
                      "syllableId": "{{syllableId}}",
                      "position": 0,
                      "beatPosition": { "bar": 2, "beat": 3, "tick": 120 }
                    }]
                  }]
                }]
              }],
              "tracks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV9);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            var line = loaded.Sections[0].LyricLines[0];
            Assert.Equal(placementId, Assert.Single(line.SyllablePlacements).Id);
            Assert.Equal(candidateId, Assert.Single(line.RhythmCandidates).Id);
            Assert.Equal(eventId, Assert.Single(line.RhythmCandidates[0].Events).Id);
            Assert.Empty(line.BreathPoints);
            Assert.Empty(loaded.Locks);
            Assert.Equal(originalV9, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV10WithoutInventingLocks()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        var lineId = LyricLineId.New();
        var wordId = LyricWordId.New();
        var syllableId = SyllableId.New();
        var phraseId = LyricPhraseId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV10 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 10,
              "title": "Lock Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{
                  "sectionId": "{{sectionId}}",
                  "start": { "bar": 1, "beat": 1, "tick": 0 },
                  "durationBars": 8
                }]
              },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": [{
                  "id": "{{lineId}}",
                  "text": "pain",
                  "words": [{
                    "id": "{{wordId}}",
                    "text": "pain",
                    "start": 0,
                    "length": 4,
                    "syllables": [{
                      "id": "{{syllableId}}",
                      "text": "pain",
                      "position": 0,
                      "source": "Manual",
                      "stress": null
                    }]
                  }],
                  "punctuation": [],
                  "phrases": [{
                    "id": "{{phraseId}}",
                    "position": 0,
                    "wordIds": ["{{wordId}}"],
                    "source": "Manual",
                    "prosody": null
                  }],
                  "syllablePlacements": [],
                  "rhythmCandidates": [],
                  "breathPoints": []
                }]
              }],
              "tracks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV10);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            Assert.Empty(loaded.Locks);
            Assert.Equal(NoteLetter.C, loaded.Key.Tonic);
            Assert.Equal(Accidental.Natural, loaded.Key.Accidental);
            Assert.Equal(ScaleMode.Major, loaded.Key.Mode);
            Assert.Equal(originalV10, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV11WithDefaultCMajorKey()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV11 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 11,
              "title": "Key Migration",
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{
                  "sectionId": "{{sectionId}}",
                  "start": { "bar": 1, "beat": 1, "tick": 0 },
                  "durationBars": 8
                }]
              },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": []
              }],
              "tracks": [],
              "locks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV11);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            Assert.Equal(NoteLetter.C, loaded.Key.Tonic);
            Assert.Equal(Accidental.Natural, loaded.Key.Accidental);
            Assert.Equal(ScaleMode.Major, loaded.Key.Mode);
            Assert.Equal(originalV11, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV12WithoutInventingHarmony()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV12 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 12,
              "title": "Harmony Migration",
              "key": { "tonic": "C", "accidental": "Natural", "mode": "Major" },
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{
                  "sectionId": "{{sectionId}}",
                  "start": { "bar": 1, "beat": 1, "tick": 0 },
                  "durationBars": 8
                }]
              },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": []
              }],
              "tracks": [],
              "locks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV12);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            Assert.Empty(loaded.Sections[0].Harmony);
            Assert.Equal(originalV12, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task SaveAndLoad_PreservesHarmonyCandidateIdentitiesAndEvents()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Harmony options");
            var section = project.AddSection(SectionKind.Chorus);
            project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0), 2);
            var candidate = project.CaptureHarmonyCandidate(section.Id, "Open chorus");

            await repository.SaveAsync(project, CancellationToken.None);
            var loaded = await repository.LoadAsync(project.Id, CancellationToken.None);

            var restored = Assert.Single(Assert.Single(loaded!.Sections).HarmonyCandidates);
            Assert.Equal(candidate.Id, restored.Id);
            Assert.Equal(candidate.Events[0].Id, Assert.Single(restored.Events).Id);
            Assert.Equal("Open chorus", restored.Label);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesSchemaV13WithoutInventingHarmonyCandidates()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var originalV13 = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 13,
              "title": "Candidate Migration",
              "key": { "tonic": "C", "accidental": "Natural", "mode": "Major" },
              "timeline": {
                "ticksPerQuarterNote": 480,
                "tempoMap": { "events": [{ "beat": 0, "beatsPerMinute": 120 }] },
                "timeSignatureMap": { "events": [{ "beat": 0, "numerator": 4, "denominator": 4 }] },
                "sectionPlacements": [{ "sectionId": "{{sectionId}}", "start": { "bar": 1, "beat": 1, "tick": 0 }, "durationBars": 8 }]
              },
              "sections": [{ "id": "{{sectionId}}", "kind": "Verse", "title": "Verse", "lyricLines": [], "harmony": [] }],
              "tracks": [],
              "locks": []
            }
            """;
            var path = Path.Combine(directory, $"{projectId}.json");
            await File.WriteAllTextAsync(path, originalV13);

            var loaded = await new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None);

            Assert.Equal(SchemaVersion.Current, loaded!.SchemaVersion);
            Assert.Empty(loaded.Sections[0].HarmonyCandidates);
            Assert.Equal(originalV13, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesBackupOfPreviousGoodProject()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("First Saved Title");
            await repository.SaveAsync(project, CancellationToken.None);
            project.Rename("Second Saved Title");
            await repository.SaveAsync(project, CancellationToken.None);

            var backupPath = Path.Combine(directory, "backups", $"{project.Id}.json");
            Assert.True(File.Exists(backupPath));
            Assert.Contains("First Saved Title", await File.ReadAllTextAsync(backupPath));
            Assert.Equal("Second Saved Title", (await repository.LoadAsync(project.Id, CancellationToken.None))!.Title);
            Assert.False(File.Exists(Path.Combine(directory, $"{project.Id}.json.tmp")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task SaveAsync_CorruptActiveFileDoesNotReplaceLastGoodBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Known Good Backup");
            await repository.SaveAsync(project, CancellationToken.None);
            project.Rename("Current Active Save");
            await repository.SaveAsync(project, CancellationToken.None);
            var backupPath = Path.Combine(directory, "backups", $"{project.Id}.json");
            var knownGoodBackup = await File.ReadAllTextAsync(backupPath);

            await File.WriteAllTextAsync(Path.Combine(directory, $"{project.Id}.json"), "{ damaged active file");
            project.Rename("Recovered In-Memory Save");
            await repository.SaveAsync(project, CancellationToken.None);

            Assert.Equal(knownGoodBackup, await File.ReadAllTextAsync(backupPath));
            Assert.Equal("Recovered In-Memory Save", (await repository.LoadAsync(project.Id, CancellationToken.None))!.Title);
            Assert.Single(Directory.EnumerateFiles(Path.Combine(directory, "recovery"), $"{project.Id}-*.json"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ListAsync_SkipsCorruptProjectAndCreatesOnlyOneRecoveryCopy()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var corruptId = ProjectId.New();
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var healthy = SongProject.Create("Healthy Song");
            await repository.SaveAsync(healthy, CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(directory, $"{corruptId}.json"), "{ damaged");

            var firstList = await repository.ListAsync(CancellationToken.None);
            var secondList = await repository.ListAsync(CancellationToken.None);

            Assert.Single(firstList);
            Assert.Equal(healthy.Id, firstList[0].Id);
            Assert.Single(secondList);
            Assert.Single(Directory.EnumerateFiles(Path.Combine(directory, "recovery"), $"{corruptId}-*.json"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task PermanentlyDelete_RemovesTrashBackupAndRecoveryArtifacts()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Erase Every Copy");
            await repository.SaveAsync(project, CancellationToken.None);
            project.Rename("Second Save");
            await repository.SaveAsync(project, CancellationToken.None);
            var activePath = Path.Combine(directory, $"{project.Id}.json");
            await File.WriteAllTextAsync(activePath, "{ damaged before deletion");
            await Assert.ThrowsAsync<CorruptProjectException>(() =>
                repository.LoadAsync(project.Id, CancellationToken.None));

            Assert.True(await repository.MoveToTrashAsync(project.Id, CancellationToken.None));
            Assert.True(await repository.PermanentlyDeleteAsync(project.Id, CancellationToken.None));

            Assert.False(File.Exists(Path.Combine(directory, "backups", $"{project.Id}.json")));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(directory, "recovery"), $"{project.Id}-*.json"));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(directory, "trash"), $"{project.Id}-*.json"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MalformedJsonCreatesRecoveryCopyAndUsefulError()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var id = ProjectId.New();
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, $"{id}.json"), "{ incomplete");

            var exception = await Assert.ThrowsAsync<CorruptProjectException>(() =>
                new JsonFileProjectRepository(directory).LoadAsync(id, CancellationToken.None));

            Assert.Equal("corrupt_project", exception.Code);
            Assert.NotNull(exception.RecoveryCopyFileName);
            Assert.True(File.Exists(Path.Combine(directory, "recovery", exception.RecoveryCopyFileName!)));
            Assert.DoesNotContain("System.Text.Json", exception.Message);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_FutureSchemaIsRejectedWithoutChangingProjectFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var id = ProjectId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var json = $$"""{ "id": "{{id}}", "schemaVersion": 99, "title": "Future Song" }""";
            var path = Path.Combine(directory, $"{id}.json");
            await File.WriteAllTextAsync(path, json);

            var exception = await Assert.ThrowsAsync<UnsupportedProjectSchemaException>(() =>
                new JsonFileProjectRepository(directory).LoadAsync(id, CancellationToken.None));

            Assert.Equal(99, exception.Version);
            Assert.Equal(SchemaVersion.Current.Value, exception.CurrentVersion);
            Assert.Equal(json, await File.ReadAllTextAsync(path));
            Assert.False(Directory.Exists(Path.Combine(directory, "recovery")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MismatchedIdentityIsRejectedAndPreservedForRecovery()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var requestedId = ProjectId.New();
        var embeddedId = ProjectId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var json = $$"""
            {
              "id": "{{embeddedId}}",
              "schemaVersion": 1,
              "title": "Wrong Identity",
              "tempo": { "beat": 0, "beatsPerMinute": 120 },
              "timeSignature": { "beat": 0, "numerator": 4, "denominator": 4 }
            }
            """;
            await File.WriteAllTextAsync(Path.Combine(directory, $"{requestedId}.json"), json);

            var exception = await Assert.ThrowsAsync<InvalidProjectDataException>(() =>
                new JsonFileProjectRepository(directory).LoadAsync(requestedId, CancellationToken.None));

            Assert.Equal("invalid_project_data", exception.Code);
            Assert.NotNull(exception.RecoveryCopyFileName);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_DuplicateLyricIdentifiersAreRejectedAndPreservedForRecovery()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        var lyricId = Guid.NewGuid();
        try
        {
            Directory.CreateDirectory(directory);
            var json = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 1,
              "title": "Duplicate Lyrics",
              "tempo": { "beat": 0, "beatsPerMinute": 120 },
              "timeSignature": { "beat": 0, "numerator": 4, "denominator": 4 },
              "sections": [{
                "id": "{{sectionId}}",
                "kind": "Verse",
                "title": "Verse",
                "lyricLines": [
                  { "id": "{{lyricId}}", "text": "First" },
                  { "id": "{{lyricId}}", "text": "Duplicate" }
                ]
              }]
            }
            """;
            await File.WriteAllTextAsync(Path.Combine(directory, $"{projectId}.json"), json);

            var exception = await Assert.ThrowsAsync<InvalidProjectDataException>(() =>
                new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None));

            Assert.Equal("invalid_project_data", exception.Code);
            Assert.NotNull(exception.RecoveryCopyFileName);
            Assert.True(File.Exists(Path.Combine(directory, "recovery", exception.RecoveryCopyFileName!)));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_DuplicateClipIdentifiersAreRejectedAndPreservedForRecovery()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        var trackId = TrackId.New();
        var clipId = ClipId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var json = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 1,
              "title": "Duplicate Clips",
              "tempo": { "beat": 0, "beatsPerMinute": 120 },
              "timeSignature": { "beat": 0, "numerator": 4, "denominator": 4 },
              "tracks": [{
                "id": "{{trackId}}",
                "name": "Guide",
                "clips": [
                  { "id": "{{clipId}}", "name": "First", "startBeat": 0, "lengthInBeats": 4 },
                  { "id": "{{clipId}}", "name": "Duplicate", "startBeat": 4, "lengthInBeats": 4 }
                ]
              }]
            }
            """;
            await File.WriteAllTextAsync(Path.Combine(directory, $"{projectId}.json"), json);

            var exception = await Assert.ThrowsAsync<InvalidProjectDataException>(() =>
                new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None));

            Assert.NotNull(exception.RecoveryCopyFileName);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"schemaVersion\": 0 }")]
    [InlineData("{ \"schemaVersion\": -1 }")]
    [InlineData("{ \"schemaVersion\": \"one\" }")]
    public async Task LoadAsync_InvalidSchemaDeclarationIsRejected(string json)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        var projectId = ProjectId.New();
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, $"{projectId}.json"), json);

            var exception = await Assert.ThrowsAsync<InvalidProjectDataException>(() =>
                new JsonFileProjectRepository(directory).LoadAsync(projectId, CancellationToken.None));

            Assert.Equal("invalid_project_data", exception.Code);
            Assert.NotNull(exception.RecoveryCopyFileName);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
