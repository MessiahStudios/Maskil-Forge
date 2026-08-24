using System.Security.Cryptography;
using System.Text;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class LoudnessGestureNoteSketchTests
{
    [Fact]
    public void Project_MapsApprovedRmsToACfourHitAndTakeRelativeTicks()
    {
        var project = SongProject.Create("Loudness sketch");
        var asset = CreateAsset();
        var observation = CreateLoudness(asset.Id, 0, 250, -18.2m, -4.1m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        var gesture = project.SetPerformanceObservationGesture(observation.Id, now);

        var sketch = LoudnessGestureNoteSketcher.Project(project, asset.Id);
        var note = Assert.Single(sketch.Events);

        Assert.Equal(asset.Id, sketch.SourceAssetId);
        Assert.Equal(0, sketch.StartTick);
        Assert.Equal(gesture.Id, note.GestureId);
        Assert.Equal(observation.Id, note.ObservationId);
        Assert.Equal(60, note.Pitch.MidiNumber);
        Assert.Equal(NoteLetter.C, note.Pitch.Letter);
        Assert.Equal(Accidental.Natural, note.Pitch.Accidental);
        Assert.Equal(4, note.Pitch.Octave);
        Assert.Equal(0, note.StartTick);
        Assert.Equal(240, note.DurationTicks);
        Assert.Equal(88, note.Velocity);
    }

    [Fact]
    public void Project_UsesCorrectedRmsWhenTheClaimIsInaccurate()
    {
        var project = SongProject.Create("Corrected loudness sketch");
        var asset = CreateAsset();
        var observation = CreateLoudness(asset.Id, 0, 250, -18.2m, -4.1m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Inaccurate, now);
        project.SetPerformanceObservationCorrection(
            observation.Id,
            [
                new PerformanceMeasurement("rmsDbfs", -12m, "dBFS"),
                new PerformanceMeasurement("peakDbfs", -4.1m, "dBFS")
            ],
            now);
        project.SetPerformanceObservationGesture(observation.Id, now);

        var note = Assert.Single(LoudnessGestureNoteSketcher.Project(project, asset.Id).Events);

        Assert.Equal(102, note.Velocity);
        Assert.Equal(0, note.StartTick);
        Assert.Equal(240, note.DurationTicks);
    }

    [Fact]
    public void Project_UsesFallbackVelocityWhenRmsIsMissing()
    {
        var project = SongProject.Create("Missing loudness RMS");
        var asset = CreateAsset();
        var observation = CreateObservation(
            asset.Id,
            "loudness.frame",
            "maskil.browser.loudness",
            0,
            250,
            [new PerformanceMeasurement("peakDbfs", -4.1m, "dBFS")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);

        var note = Assert.Single(LoudnessGestureNoteSketcher.Project(project, asset.Id).Events);

        Assert.Equal(60, note.Pitch.MidiNumber);
        Assert.Equal(96, note.Velocity);
    }

    [Fact]
    public void Project_RejectsRmsOutsideAnalyzerBounds()
    {
        var project = SongProject.Create("Invalid loudness RMS");
        var asset = CreateAsset();
        var observation = CreateObservation(
            asset.Id,
            "loudness.frame",
            "maskil.browser.loudness",
            0,
            250,
            [
                new PerformanceMeasurement("rmsDbfs", 6m, "dBFS"),
                new PerformanceMeasurement("peakDbfs", 6m, "dBFS")
            ]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => LoudnessGestureNoteSketcher.Project(project, asset.Id));

        Assert.Contains("Loudness-gesture RMS must be between -120 and 0 dBFS", error.Message);
    }

    [Fact]
    public void Project_IgnoresPitchAndOnsetGestures()
    {
        var project = SongProject.Create("Non-loudness gestures");
        var asset = CreateAsset();
        var pitch = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 200, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var onset = CreateObservation(asset.Id, "onset.event", "maskil.browser.onset-energy", 96, 32,
            [new PerformanceMeasurement("strength", 0.8m, "normalized")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(pitch);
        project.RegisterPerformanceObservation(onset);
        project.SetPerformanceObservationReview(pitch.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationReview(onset.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(pitch.Id, now);
        project.SetPerformanceObservationGesture(onset.Id, now);

        var error = Assert.Throws<InvalidOperationException>(() => LoudnessGestureNoteSketcher.Project(project, asset.Id));

        Assert.Contains("Promote at least one loudness claim", error.Message);
    }

    [Fact]
    public void Project_OffsetsTakeRelativeTicksByTheArtistPlacement()
    {
        var project = SongProject.Create("Placed loudness sketch");
        var asset = CreateAsset();
        var observation = CreateLoudness(asset.Id, 0, 250, -18.2m, -4.1m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        project.SetVocalTakePlacement(asset.Id, new MusicalPosition(9, 1, 0));

        var sketch = LoudnessGestureNoteSketcher.Project(project, asset.Id);
        var note = Assert.Single(sketch.Events);

        Assert.Equal(15_360, sketch.StartTick);
        Assert.Equal(15_360, note.StartTick);
        Assert.Equal(240, note.DurationTicks);
    }

    [Fact]
    public void UseLoudnessGestureNoteSketchCommand_IsExplicitAdditiveAndReversible()
    {
        var project = SongProject.Create("Accepted loudness sketch");
        var asset = CreateAsset();
        var observation = CreateLoudness(asset.Id, 0, 250, -18.2m, -4.1m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        var manual = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 120, 70);
        var editor = new ProjectEditor(project);
        var command = new UseLoudnessGestureNoteSketchCommand(asset.Id);

        editor.Execute(command);
        var accepted = Assert.Single(editor.Project.NoteEvents, item => item.Id != manual.Id);
        Assert.Equal(60, accepted.Pitch.MidiNumber);
        Assert.Equal(0, accepted.StartTick);
        Assert.Equal(240, accepted.DurationTicks);
        Assert.Equal(88, accepted.Velocity);
        Assert.Contains(editor.Project.NoteEvents, item => item.Id == manual.Id);

        editor.Undo();
        Assert.Equal(manual.Id, Assert.Single(editor.Project.NoteEvents).Id);

        editor.Redo();
        Assert.Equal(accepted.Id, Assert.Single(editor.Project.NoteEvents, item => item.Id != manual.Id).Id);

        project.ClearPerformanceObservationGesture(observation.Id);
        Assert.Equal(accepted.Id, Assert.Single(editor.Project.NoteEvents, item => item.Id != manual.Id).Id);
    }

    [Fact]
    public void ChangingPlacement_DoesNotMoveAlreadyAcceptedLoudnessNotes()
    {
        var project = SongProject.Create("Stable loudness notes");
        var asset = CreateAsset();
        var observation = CreateLoudness(asset.Id, 0, 250, -18.2m, -4.1m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        var editor = new ProjectEditor(project);
        editor.Execute(new UseLoudnessGestureNoteSketchCommand(asset.Id));
        var accepted = Assert.Single(editor.Project.NoteEvents);
        Assert.Equal(0, accepted.StartTick);

        editor.Execute(new SetVocalTakePlacementCommand(asset.Id, new MusicalPosition(9, 1, 0)));

        Assert.Equal(accepted.Id, Assert.Single(editor.Project.NoteEvents).Id);
        Assert.Equal(0, accepted.StartTick);
        Assert.Equal(15_360, Assert.Single(LoudnessGestureNoteSketcher.Project(project, asset.Id).Events).StartTick);
    }

    [Fact]
    public void Project_RequiresAnExistingOriginalVocalTake()
    {
        var project = SongProject.Create("Missing take");

        Assert.Throws<KeyNotFoundException>(() => LoudnessGestureNoteSketcher.Project(project, ProjectAssetId.New()));
    }

    private static ProjectAsset CreateAsset()
    {
        var content = Encoding.UTF8.GetBytes("artist-loudness source performance");
        return new ProjectAsset(
            ProjectAssetId.New(),
            ProjectAssetKind.OriginalVocalTake,
            "audio/webm",
            content.LongLength,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            "Loudness take");
    }

    private static PerformanceObservation CreateLoudness(
        ProjectAssetId assetId,
        long startMilliseconds,
        long durationMilliseconds,
        decimal rmsDecibels,
        decimal peakDecibels) =>
        CreateObservation(
            assetId,
            "loudness.frame",
            "maskil.browser.loudness",
            startMilliseconds,
            durationMilliseconds,
            [
                new PerformanceMeasurement("rmsDbfs", rmsDecibels, "dBFS"),
                new PerformanceMeasurement("peakDbfs", peakDecibels, "dBFS")
            ]);

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
