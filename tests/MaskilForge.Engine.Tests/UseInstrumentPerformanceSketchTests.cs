using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class UseInstrumentPerformanceSketchTests
{
    [Fact]
    public void Command_AddsInRangeCelloSlidesToTheNamedPartAndStoresSwellsAsATaggedCurve()
    {
        var editor = SeedAssignedPart("Cello persist", "cello", out var asset, out var part);
        PromotePitch(editor.Project, asset.Id, 200, 440m);
        PromoteLoudness(editor.Project, asset.Id, 200, -18.2m);
        var existingNoteId = Assert.Single(part.NoteEventIds);

        editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "cello", part.Id));

        var accepted = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal(part.Id, accepted.Id);
        Assert.Equal("cello", accepted.InstrumentProfileId);
        Assert.Equal(2, accepted.NoteEventIds.Count);
        Assert.Contains(existingNoteId, accepted.NoteEventIds);
        var added = Assert.Single(editor.Project.NoteEvents, item => item.Id != existingNoteId);
        Assert.Equal(69, added.Pitch.MidiNumber);
        Assert.Equal(192, added.StartTick);
        Assert.Equal(77, added.DurationTicks);
        Assert.Equal(96, added.Velocity);
        Assert.Contains(added.Id, accepted.NoteEventIds);

        var curve = Assert.Single(editor.Project.ExpressionCurves);
        Assert.Equal("Cello swell", curve.Name);
        Assert.Equal(ExpressionCurveKind.Dynamics, curve.Kind);
        Assert.Equal("cello", curve.InstrumentProfileId);
        Assert.Equal(192, Assert.Single(curve.Points).Tick);
        Assert.Equal(88, Assert.Single(curve.Points).Value);
    }

    [Fact]
    public void Command_SkipsOutOfRangeSlidesWithoutTransposingThem()
    {
        var editor = SeedAssignedPart("Cello range persist", "cello", out var asset, out var part);
        PromotePitch(editor.Project, asset.Id, 0, 987.77m);
        PromotePitch(editor.Project, asset.Id, 100, 73.42m);
        var existingCount = editor.Project.NoteEvents.Count;

        editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "cello", part.Id));

        Assert.Equal(existingCount + 1, editor.Project.NoteEvents.Count);
        var added = Assert.Single(editor.Project.NoteEvents, item => item.StartTick == 96);
        Assert.Equal(38, added.Pitch.MidiNumber);
        Assert.DoesNotContain(editor.Project.NoteEvents, item => item.Pitch.MidiNumber == 83);
        Assert.Empty(editor.Project.ExpressionCurves);
    }

    [Fact]
    public void Command_StoresGuitarSlidesAndSwellsAgainstANamedGuitarPart()
    {
        var editor = SeedAssignedPart("Guitar persist", "acoustic-guitar", out var asset, out var part);
        PromotePitch(editor.Project, asset.Id, 200, 440m);
        PromoteLoudness(editor.Project, asset.Id, 0, -18.2m);

        editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "acoustic-guitar", part.Id));

        var added = Assert.Single(editor.Project.NoteEvents, item => item.Pitch.MidiNumber == 69);
        Assert.Contains(added.Id, Assert.Single(editor.Project.MusicalParts).NoteEventIds);
        var curve = Assert.Single(editor.Project.ExpressionCurves);
        Assert.Equal("Acoustic Guitar swell", curve.Name);
        Assert.Equal("acoustic-guitar", curve.InstrumentProfileId);
    }

    [Fact]
    public void Command_UndoAndRedoRestoreNotesPartMembershipAndCurve()
    {
        var editor = SeedAssignedPart("Undo persist", "cello", out var asset, out var part);
        PromotePitch(editor.Project, asset.Id, 200, 440m);
        PromoteLoudness(editor.Project, asset.Id, 200, -18.2m);
        var existingNoteId = Assert.Single(part.NoteEventIds);

        editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "cello", part.Id));
        var addedNoteId = Assert.Single(editor.Project.NoteEvents, item => item.Id != existingNoteId).Id;
        var curveId = Assert.Single(editor.Project.ExpressionCurves).Id;

        editor.Undo();
        var restored = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal([existingNoteId], restored.NoteEventIds);
        Assert.Equal("cello", restored.InstrumentProfileId);
        Assert.DoesNotContain(editor.Project.NoteEvents, item => item.Id == addedNoteId);
        Assert.Empty(editor.Project.ExpressionCurves);

        editor.Redo();
        Assert.Contains(addedNoteId, Assert.Single(editor.Project.MusicalParts).NoteEventIds);
        Assert.Equal(curveId, Assert.Single(editor.Project.ExpressionCurves).Id);
        Assert.Equal("cello", Assert.Single(editor.Project.ExpressionCurves).InstrumentProfileId);
    }

    [Fact]
    public void Command_StoresPianoAndBassSwellsWithoutInventingSlideNotes()
    {
        foreach (var (instrumentId, curveName) in new[] { ("piano", "Piano swell"), ("electric-bass", "Electric Bass swell") })
        {
            var editor = SeedAssignedPart($"Persist {instrumentId}", instrumentId, out var asset, out var part);
            PromotePitch(editor.Project, asset.Id, 200, 440m);
            PromoteLoudness(editor.Project, asset.Id, 0, -18.2m);
            var existingNoteId = Assert.Single(part.NoteEventIds);

            editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, instrumentId, part.Id));

            Assert.Equal([existingNoteId], Assert.Single(editor.Project.MusicalParts).NoteEventIds);
            Assert.Equal(existingNoteId, Assert.Single(editor.Project.NoteEvents).Id);
            var curve = Assert.Single(editor.Project.ExpressionCurves);
            Assert.Equal(curveName, curve.Name);
            Assert.Equal(instrumentId, curve.InstrumentProfileId);
            Assert.Equal(88, Assert.Single(curve.Points).Value);
        }
    }

    [Fact]
    public void Command_ThrowsWhenPianoOrKitHasNothingPersistable()
    {
        foreach (var instrumentId in new[] { "piano", "drum-kit" })
        {
            var editor = SeedAssignedPart($"Empty {instrumentId}", instrumentId, out var asset, out var part);
            PromotePitch(editor.Project, asset.Id, 200, 440m);

            var error = Assert.Throws<InvalidOperationException>(() =>
                editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, instrumentId, part.Id)));

            Assert.Contains("no in-range slides, swells, or hits", error.Message);
            Assert.Single(editor.Project.NoteEvents);
            Assert.Empty(editor.Project.ExpressionCurves);
        }
    }

    [Fact]
    public void Command_StoresKitHitsOnANamedKitPartWithoutInventingSwellOrSlide()
    {
        var editor = SeedAssignedPart("Kit persist", "drum-kit", out var asset, out var part);
        PromoteOnset(editor.Project, asset.Id, 96, 0.8m);
        PromotePitch(editor.Project, asset.Id, 200, 440m);
        var existingNoteId = Assert.Single(part.NoteEventIds);

        editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "drum-kit", part.Id));

        var accepted = Assert.Single(editor.Project.MusicalParts);
        Assert.Equal(part.Id, accepted.Id);
        Assert.Equal("drum-kit", accepted.InstrumentProfileId);
        Assert.Equal(2, accepted.NoteEventIds.Count);
        Assert.Contains(existingNoteId, accepted.NoteEventIds);
        var added = Assert.Single(editor.Project.NoteEvents, item => item.Id != existingNoteId);
        Assert.Equal(60, added.Pitch.MidiNumber);
        Assert.Equal(92, added.StartTick);
        Assert.Equal(31, added.DurationTicks);
        Assert.Equal(102, added.Velocity);
        Assert.Contains(added.Id, accepted.NoteEventIds);
        Assert.Empty(editor.Project.ExpressionCurves);
        Assert.DoesNotContain(editor.Project.NoteEvents, item => item.Pitch.MidiNumber == 69);
    }

    [Fact]
    public void Command_StoresWaveTwoSwellsAndViolinSlidesWithoutInventingWindSlidesOrKitHits()
    {
        var violin = SeedAssignedPart("Violin persist", "violin", out var violinAsset, out var violinPart);
        PromotePitch(violin.Project, violinAsset.Id, 200, 440m);
        PromoteLoudness(violin.Project, violinAsset.Id, 200, -18.2m);
        PromoteOnset(violin.Project, violinAsset.Id, 96, 0.8m);
        var violinNoteId = Assert.Single(violinPart.NoteEventIds);

        violin.Execute(new UseInstrumentPerformanceSketchCommand(violinAsset.Id, "violin", violinPart.Id));

        var added = Assert.Single(violin.Project.NoteEvents, item => item.Id != violinNoteId);
        Assert.Equal(69, added.Pitch.MidiNumber);
        Assert.Equal(96, added.Velocity);
        Assert.Contains(added.Id, Assert.Single(violin.Project.MusicalParts).NoteEventIds);
        Assert.DoesNotContain(violin.Project.NoteEvents, item => item.Pitch.MidiNumber == 60 && item.StartTick == 92);
        var violinCurve = Assert.Single(violin.Project.ExpressionCurves);
        Assert.Equal("Violin swell", violinCurve.Name);
        Assert.Equal("violin", violinCurve.InstrumentProfileId);

        foreach (var (instrumentId, curveName) in new[]
        {
            ("flute", "Flute swell"),
            ("clarinet", "Clarinet swell"),
            ("trumpet", "Trumpet swell"),
        })
        {
            var editor = SeedAssignedPart($"Persist {instrumentId}", instrumentId, out var asset, out var part);
            PromotePitch(editor.Project, asset.Id, 200, 440m);
            PromoteLoudness(editor.Project, asset.Id, 0, -18.2m);
            PromoteOnset(editor.Project, asset.Id, 96, 0.8m);
            var existingNoteId = Assert.Single(part.NoteEventIds);

            editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, instrumentId, part.Id));

            Assert.Equal([existingNoteId], Assert.Single(editor.Project.MusicalParts).NoteEventIds);
            Assert.Equal(existingNoteId, Assert.Single(editor.Project.NoteEvents).Id);
            var curve = Assert.Single(editor.Project.ExpressionCurves);
            Assert.Equal(curveName, curve.Name);
            Assert.Equal(instrumentId, curve.InstrumentProfileId);
            Assert.Equal(88, Assert.Single(curve.Points).Value);
        }
    }

    [Fact]
    public void Command_IgnoresOnsetsWhenStoringACelloSketch()
    {
        var editor = SeedAssignedPart("Cello ignores hits", "cello", out var asset, out var part);
        PromoteOnset(editor.Project, asset.Id, 96, 0.8m);
        PromotePitch(editor.Project, asset.Id, 200, 440m);
        var existingNoteId = Assert.Single(part.NoteEventIds);

        editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "cello", part.Id));

        Assert.Equal(2, Assert.Single(editor.Project.MusicalParts).NoteEventIds.Count);
        var added = Assert.Single(editor.Project.NoteEvents, item => item.Id != existingNoteId);
        Assert.Equal(69, added.Pitch.MidiNumber);
        Assert.DoesNotContain(editor.Project.NoteEvents, item => item.Pitch.MidiNumber == 60 && item.StartTick == 92);
        Assert.Empty(editor.Project.ExpressionCurves);
    }

    [Fact]
    public void Command_ThrowsWhenKitHitsSitOutsideThePartSection()
    {
        var editor = SeedAssignedPart("Kit outside section", "drum-kit", out var asset, out var part);
        PromoteOnset(editor.Project, asset.Id, 96, 0.8m);
        editor.Project.SetVocalTakePlacement(asset.Id, new MusicalPosition(9, 1, 0));

        var error = Assert.Throws<ArgumentException>(() =>
            editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "drum-kit", part.Id)));

        Assert.Contains("hits begin inside the named part's section", error.Message);
        Assert.Single(editor.Project.NoteEvents);
        Assert.Empty(editor.Project.ExpressionCurves);
    }

    [Fact]
    public void Command_RequiresThePartToAlreadyNameTheSameInstrument()
    {
        var editor = SeedAssignedPart("Unassigned persist", null, out var asset, out var part);
        PromotePitch(editor.Project, asset.Id, 200, 440m);

        var unassigned = Assert.Throws<ArgumentException>(() =>
            editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "cello", part.Id)));
        Assert.Contains("Name Cello on this musical part", unassigned.Message);
        Assert.Single(editor.Project.NoteEvents);

        editor.Execute(new SetMusicalPartCommand(part.Id, part.Label, part.NoteEventIds, "acoustic-guitar"));
        var mismatched = Assert.Throws<ArgumentException>(() =>
            editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "cello", part.Id)));
        Assert.Contains("Acoustic Guitar", mismatched.Message);
        Assert.Contains("Cello", mismatched.Message);
        Assert.Equal("acoustic-guitar", Assert.Single(editor.Project.MusicalParts).InstrumentProfileId);
    }

    [Fact]
    public void Command_ThrowsWhenInRangeSlidesSitOutsideThePartSection()
    {
        var editor = SeedAssignedPart("Outside section", "cello", out var asset, out var part);
        PromotePitch(editor.Project, asset.Id, 200, 440m);
        editor.Project.SetVocalTakePlacement(asset.Id, new MusicalPosition(9, 1, 0));

        var error = Assert.Throws<ArgumentException>(() =>
            editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "cello", part.Id)));

        Assert.Contains("inside the named part's section", error.Message);
        Assert.Single(editor.Project.NoteEvents);
        Assert.Empty(editor.Project.ExpressionCurves);
    }

    [Fact]
    public void Command_ThrowsWhenNothingIsPersistable()
    {
        var editor = SeedAssignedPart("Empty persist", "cello", out var asset, out var part);
        PromotePitch(editor.Project, asset.Id, 0, 987.77m);

        var error = Assert.Throws<InvalidOperationException>(() =>
            editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "cello", part.Id)));

        Assert.Contains("no in-range slides, swells, or hits", error.Message);
        Assert.Single(editor.Project.NoteEvents);
        Assert.Empty(editor.Project.ExpressionCurves);
        Assert.Equal(Assert.Single(part.NoteEventIds), Assert.Single(editor.Project.MusicalParts).NoteEventIds.Single());
    }

    [Fact]
    public void Schema30_MigratesExistingCurvesToUnassignedInstrumentsWithoutInventingThem()
    {
        var project = SongProject.Create("Schema 30 curves");
        project.AddExpressionCurve("Take dynamics", ExpressionCurveKind.Dynamics, [new ExpressionCurvePoint(0, 88)], "cello");
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        document["schemaVersion"] = 30;
        foreach (var curve in document["expressionCurves"]!.AsArray().OfType<JsonObject>())
            curve.Remove("instrumentProfileId");

        var inspected = PortableProjectImporter.Inspect(document.ToJsonString());

        Assert.Equal(30, inspected.SourceSchemaVersion);
        Assert.Equal(SchemaVersion.Current.Value, inspected.Project.SchemaVersion.Value);
        Assert.Null(Assert.Single(inspected.Project.ExpressionCurves).InstrumentProfileId);
        Assert.Equal("Take dynamics", Assert.Single(inspected.Project.ExpressionCurves).Name);
    }

    [Fact]
    public async Task SaveLoad_PreservesAssignedInstrumentOnAnAcceptedSwellCurve()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var editor = SeedAssignedPart("Saved cello swell", "cello", out var asset, out var part);
            PromoteLoudness(editor.Project, asset.Id, 0, -18.2m);
            var repository = new JsonFileProjectRepository(directory);
            await repository.SaveWithAssetAsync(
                editor.Project,
                asset,
                new MemoryStream(Encoding.UTF8.GetBytes("artist-retargeted source performance")));
            editor.Execute(new UseInstrumentPerformanceSketchCommand(asset.Id, "cello", part.Id));
            await repository.SaveAsync(editor.Project);

            var loaded = await repository.LoadAsync(editor.Project.Id);

            Assert.NotNull(loaded);
            Assert.Equal(SchemaVersion.Current, loaded.SchemaVersion);
            var restored = Assert.Single(loaded.ExpressionCurves);
            Assert.Equal("cello", restored.InstrumentProfileId);
            Assert.Equal("Cello swell", restored.Name);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static ProjectEditor SeedAssignedPart(
        string title,
        string? instrumentId,
        out ProjectAsset asset,
        out MusicalPart part)
    {
        var editor = new ProjectEditor(SongProject.Create(title));
        var section = editor.Project.AddSection(SectionKind.Verse);
        editor.Project.SetSectionRole(section.Id, ArrangementRole.Harmony);
        var note = editor.Project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 80);
        part = editor.Project.AddMusicalPart(section.Id, ArrangementRole.Harmony, "Verse harmony", [note.Id], instrumentId);
        asset = CreateAsset();
        editor.Project.RegisterAsset(asset);
        return editor;
    }

    private static void PromotePitch(SongProject project, ProjectAssetId assetId, long startMilliseconds, decimal frequencyHertz)
    {
        var observation = CreateObservation(
            assetId,
            "pitch.frame",
            "maskil.browser.pitch-acf",
            startMilliseconds,
            80,
            [new PerformanceMeasurement("frequencyHertz", frequencyHertz, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
    }

    private static void PromoteOnset(SongProject project, ProjectAssetId assetId, long startMilliseconds, decimal strength)
    {
        var observation = CreateObservation(
            assetId,
            "onset.event",
            "maskil.browser.onset-energy",
            startMilliseconds,
            32,
            [
                new PerformanceMeasurement("strength", strength, "normalized"),
                new PerformanceMeasurement("confidence", 0.9m, "normalized")
            ]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
    }

    private static void PromoteLoudness(SongProject project, ProjectAssetId assetId, long startMilliseconds, decimal rmsDbfs)
    {
        var observation = CreateObservation(
            assetId,
            "loudness.frame",
            "maskil.browser.loudness",
            startMilliseconds,
            80,
            [
                new PerformanceMeasurement("rmsDbfs", rmsDbfs, "dBFS"),
                new PerformanceMeasurement("peakDbfs", -4.1m, "dBFS")
            ]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
    }

    private static ProjectAsset CreateAsset()
    {
        var content = Encoding.UTF8.GetBytes("artist-retargeted source performance");
        return new ProjectAsset(
            ProjectAssetId.New(),
            ProjectAssetKind.OriginalVocalTake,
            "audio/webm",
            content.LongLength,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            "Retarget take");
    }

    private static PerformanceObservation CreateObservation(
        ProjectAssetId assetId,
        string kind,
        string analyzerId,
        long startMilliseconds,
        long durationMilliseconds,
        IReadOnlyList<PerformanceMeasurement> measurements) => new(
        PerformanceObservationId.New(),
        assetId,
        kind,
        startMilliseconds,
        durationMilliseconds,
        measurements,
        0.8m,
        analyzerId,
        "1.0.0",
        PerformanceObservationProvenance.DeterministicAnalyzer,
        DateTimeOffset.UtcNow);
}
