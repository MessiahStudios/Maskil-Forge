using System.Security.Cryptography;
using System.Text;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class OnsetGestureNoteSketchTests
{
    [Fact]
    public void Project_MapsApprovedOnsetStrengthToACfourHitAndTakeRelativeTicks()
    {
        var project = SongProject.Create("Onset sketch");
        var asset = CreateAsset();
        var observation = CreateOnset(asset.Id, 96, 32, 0.8m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        var gesture = project.SetPerformanceObservationGesture(observation.Id, now);

        var sketch = OnsetGestureNoteSketcher.Project(project, asset.Id);
        var note = Assert.Single(sketch.Events);

        Assert.Equal(asset.Id, sketch.SourceAssetId);
        Assert.Equal(0, sketch.StartTick);
        Assert.Equal(gesture.Id, note.GestureId);
        Assert.Equal(observation.Id, note.ObservationId);
        Assert.Equal(60, note.Pitch.MidiNumber);
        Assert.Equal(NoteLetter.C, note.Pitch.Letter);
        Assert.Equal(Accidental.Natural, note.Pitch.Accidental);
        Assert.Equal(4, note.Pitch.Octave);
        Assert.Equal(92, note.StartTick);
        Assert.Equal(31, note.DurationTicks);
        Assert.Equal(102, note.Velocity);
    }

    [Fact]
    public void Project_UsesCorrectedStrengthWhenTheClaimIsInaccurate()
    {
        var project = SongProject.Create("Corrected onset sketch");
        var asset = CreateAsset();
        var observation = CreateOnset(asset.Id, 0, 32, 0.4m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Inaccurate, now);
        project.SetPerformanceObservationCorrection(
            observation.Id, [new PerformanceMeasurement("strength", 1m, "normalized")], now);
        project.SetPerformanceObservationGesture(observation.Id, now);

        var note = Assert.Single(OnsetGestureNoteSketcher.Project(project, asset.Id).Events);

        Assert.Equal(127, note.Velocity);
        Assert.Equal(0, note.StartTick);
        Assert.Equal(31, note.DurationTicks);
    }

    [Fact]
    public void Project_UsesFallbackVelocityWhenStrengthIsMissing()
    {
        var project = SongProject.Create("Missing onset strength");
        var asset = CreateAsset();
        var observation = CreateObservation(
            asset.Id,
            "onset.event",
            "maskil.browser.onset-energy",
            96,
            32,
            [new PerformanceMeasurement("confidence", 0.9m, "normalized")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);

        var note = Assert.Single(OnsetGestureNoteSketcher.Project(project, asset.Id).Events);

        Assert.Equal(60, note.Pitch.MidiNumber);
        Assert.Equal(96, note.Velocity);
    }

    [Fact]
    public void Project_RejectsStrengthOutsideZeroToOne()
    {
        var project = SongProject.Create("Invalid onset strength");
        var asset = CreateAsset();
        var observation = CreateObservation(
            asset.Id,
            "onset.event",
            "maskil.browser.onset-energy",
            96,
            32,
            [new PerformanceMeasurement("strength", 1.5m, "normalized")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => OnsetGestureNoteSketcher.Project(project, asset.Id));

        Assert.Contains("Onset-gesture strength must be between 0 and 1", error.Message);
    }

    [Fact]
    public void Project_IgnoresPitchAndLoudnessGestures()
    {
        var project = SongProject.Create("Non-onset gestures");
        var asset = CreateAsset();
        var pitch = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 200, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var loudness = CreateObservation(asset.Id, "loudness.frame", "maskil.browser.loudness", 0, 250,
            [
                new PerformanceMeasurement("rmsDbfs", -18.2m, "dBFS"),
                new PerformanceMeasurement("peakDbfs", -4.1m, "dBFS")
            ]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(pitch);
        project.RegisterPerformanceObservation(loudness);
        project.SetPerformanceObservationReview(pitch.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationReview(loudness.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(pitch.Id, now);
        project.SetPerformanceObservationGesture(loudness.Id, now);

        var error = Assert.Throws<InvalidOperationException>(() => OnsetGestureNoteSketcher.Project(project, asset.Id));

        Assert.Contains("Promote at least one onset claim", error.Message);
    }

    [Fact]
    public void Project_OffsetsTakeRelativeTicksByTheArtistPlacement()
    {
        var project = SongProject.Create("Placed onset sketch");
        var asset = CreateAsset();
        var observation = CreateOnset(asset.Id, 96, 32, 0.8m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        project.SetVocalTakePlacement(asset.Id, new MusicalPosition(9, 1, 0));

        var sketch = OnsetGestureNoteSketcher.Project(project, asset.Id);
        var note = Assert.Single(sketch.Events);

        Assert.Equal(15_360, sketch.StartTick);
        Assert.Equal(15_452, note.StartTick);
        Assert.Equal(31, note.DurationTicks);
    }

    [Fact]
    public void UseOnsetGestureNoteSketchCommand_IsExplicitAdditiveAndReversible()
    {
        var project = SongProject.Create("Accepted onset sketch");
        var asset = CreateAsset();
        var observation = CreateOnset(asset.Id, 96, 32, 0.8m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        var manual = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 120, 70);
        var editor = new ProjectEditor(project);
        var command = new UseOnsetGestureNoteSketchCommand(asset.Id);

        editor.Execute(command);
        var accepted = Assert.Single(editor.Project.NoteEvents, item => item.Id != manual.Id);
        Assert.Equal(60, accepted.Pitch.MidiNumber);
        Assert.Equal(92, accepted.StartTick);
        Assert.Equal(31, accepted.DurationTicks);
        Assert.Equal(102, accepted.Velocity);
        Assert.Contains(editor.Project.NoteEvents, item => item.Id == manual.Id);

        editor.Undo();
        Assert.Equal(manual.Id, Assert.Single(editor.Project.NoteEvents).Id);

        editor.Redo();
        Assert.Equal(accepted.Id, Assert.Single(editor.Project.NoteEvents, item => item.Id != manual.Id).Id);

        project.ClearPerformanceObservationGesture(observation.Id);
        Assert.Equal(accepted.Id, Assert.Single(editor.Project.NoteEvents, item => item.Id != manual.Id).Id);
    }

    [Fact]
    public void ChangingPlacement_DoesNotMoveAlreadyAcceptedOnsetNotes()
    {
        var project = SongProject.Create("Stable onset notes");
        var asset = CreateAsset();
        var observation = CreateOnset(asset.Id, 96, 32, 0.8m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        var editor = new ProjectEditor(project);
        editor.Execute(new UseOnsetGestureNoteSketchCommand(asset.Id));
        var accepted = Assert.Single(editor.Project.NoteEvents);
        Assert.Equal(92, accepted.StartTick);

        editor.Execute(new SetVocalTakePlacementCommand(asset.Id, new MusicalPosition(9, 1, 0)));

        Assert.Equal(accepted.Id, Assert.Single(editor.Project.NoteEvents).Id);
        Assert.Equal(92, accepted.StartTick);
        Assert.Equal(15_452, Assert.Single(OnsetGestureNoteSketcher.Project(project, asset.Id).Events).StartTick);
    }

    [Fact]
    public void Project_RequiresAnExistingOriginalVocalTake()
    {
        var project = SongProject.Create("Missing take");

        Assert.Throws<KeyNotFoundException>(() => OnsetGestureNoteSketcher.Project(project, ProjectAssetId.New()));
    }

    private static ProjectAsset CreateAsset()
    {
        var content = Encoding.UTF8.GetBytes("artist-onset source performance");
        return new ProjectAsset(
            ProjectAssetId.New(),
            ProjectAssetKind.OriginalVocalTake,
            "audio/webm",
            content.LongLength,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            "Onset take");
    }

    private static PerformanceObservation CreateOnset(
        ProjectAssetId assetId,
        long startMilliseconds,
        long durationMilliseconds,
        decimal strength) =>
        CreateObservation(
            assetId,
            "onset.event",
            "maskil.browser.onset-energy",
            startMilliseconds,
            durationMilliseconds,
            [new PerformanceMeasurement("strength", strength, "normalized")]);

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
