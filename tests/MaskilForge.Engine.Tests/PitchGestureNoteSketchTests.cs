using System.Security.Cryptography;
using System.Text;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class PitchGestureNoteSketchTests
{
    [Fact]
    public void Project_MapsApprovedPitchFrequencyToNearestNoteAndTakeRelativeTicks()
    {
        var project = SongProject.Create("Pitch sketch");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 200, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        var gesture = project.SetPerformanceObservationGesture(observation.Id, now);

        var sketch = PitchGestureNoteSketcher.Project(project, asset.Id);
        var note = Assert.Single(sketch.Events);

        Assert.Equal(asset.Id, sketch.SourceAssetId);
        Assert.Equal(0, sketch.StartTick);
        Assert.Equal(gesture.Id, note.GestureId);
        Assert.Equal(observation.Id, note.ObservationId);
        Assert.Equal(69, note.Pitch.MidiNumber);
        Assert.Equal(NoteLetter.A, note.Pitch.Letter);
        Assert.Equal(Accidental.Natural, note.Pitch.Accidental);
        Assert.Equal(4, note.Pitch.Octave);
        Assert.Equal(192, note.StartTick);
        Assert.Equal(77, note.DurationTicks);
        Assert.Equal(96, note.Velocity);
    }

    [Fact]
    public void Project_UsesCorrectedFrequencyWhenTheClaimIsInaccurate()
    {
        var project = SongProject.Create("Corrected pitch sketch");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 0, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Inaccurate, now);
        project.SetPerformanceObservationCorrection(
            observation.Id, [new PerformanceMeasurement("frequencyHertz", 220m, "hertz")], now);
        project.SetPerformanceObservationGesture(observation.Id, now);

        var note = Assert.Single(PitchGestureNoteSketcher.Project(project, asset.Id).Events);

        Assert.Equal(57, note.Pitch.MidiNumber);
        Assert.Equal(NoteLetter.A, note.Pitch.Letter);
        Assert.Equal(3, note.Pitch.Octave);
        Assert.Equal(0, note.StartTick);
        Assert.Equal(77, note.DurationTicks);
    }

    [Fact]
    public void Project_IgnoresLoudnessAndOnsetGestures()
    {
        var project = SongProject.Create("Non-pitch gestures");
        var asset = CreateAsset();
        var loudness = CreateObservation(asset.Id, "loudness.frame", "maskil.browser.loudness", 0, 250,
            [
                new PerformanceMeasurement("rmsDbfs", -18.2m, "dBFS"),
                new PerformanceMeasurement("peakDbfs", -4.1m, "dBFS")
            ]);
        var onset = CreateObservation(asset.Id, "onset.event", "maskil.browser.onset-energy", 96, 32,
            [
                new PerformanceMeasurement("strength", 0.8m, "normalized"),
                new PerformanceMeasurement("confidence", 0.9m, "normalized")
            ]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(loudness);
        project.RegisterPerformanceObservation(onset);
        project.SetPerformanceObservationReview(loudness.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationReview(onset.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(loudness.Id, now);
        project.SetPerformanceObservationGesture(onset.Id, now);

        var error = Assert.Throws<InvalidOperationException>(() => PitchGestureNoteSketcher.Project(project, asset.Id));

        Assert.Contains("Promote at least one pitch claim", error.Message);
    }

    [Fact]
    public void Project_RequiresAnExistingOriginalVocalTake()
    {
        var project = SongProject.Create("Missing take");

        Assert.Throws<KeyNotFoundException>(() => PitchGestureNoteSketcher.Project(project, ProjectAssetId.New()));
    }

    [Fact]
    public void UsePitchGestureNoteSketchCommand_IsExplicitAdditiveAndReversible()
    {
        var project = SongProject.Create("Accepted pitch sketch");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 200, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        var manual = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 120, 70);
        var editor = new ProjectEditor(project);
        var command = new UsePitchGestureNoteSketchCommand(asset.Id);

        editor.Execute(command);
        var accepted = Assert.Single(editor.Project.NoteEvents, item => item.Id != manual.Id);
        Assert.Equal(69, accepted.Pitch.MidiNumber);
        Assert.Equal(192, accepted.StartTick);
        Assert.Equal(77, accepted.DurationTicks);
        Assert.Equal(96, accepted.Velocity);
        Assert.Contains(editor.Project.NoteEvents, item => item.Id == manual.Id);

        editor.Undo();
        Assert.Equal(manual.Id, Assert.Single(editor.Project.NoteEvents).Id);

        editor.Redo();
        Assert.Equal(accepted.Id, Assert.Single(editor.Project.NoteEvents, item => item.Id != manual.Id).Id);

        project.ClearPerformanceObservationGesture(observation.Id);
        Assert.Equal(accepted.Id, Assert.Single(editor.Project.NoteEvents, item => item.Id != manual.Id).Id);
    }

    [Fact]
    public void Project_OffsetsTakeRelativeTicksByTheArtistPlacement()
    {
        var project = SongProject.Create("Placed pitch sketch");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 200, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        project.SetVocalTakePlacement(asset.Id, new MusicalPosition(9, 1, 0));

        var sketch = PitchGestureNoteSketcher.Project(project, asset.Id);
        var note = Assert.Single(sketch.Events);

        Assert.Equal(15_360, sketch.StartTick);
        Assert.Equal(15_552, note.StartTick);
        Assert.Equal(77, note.DurationTicks);
    }

    [Fact]
    public void ChangingPlacement_DoesNotMoveAlreadyAcceptedNotes()
    {
        var project = SongProject.Create("Stable accepted notes");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 200, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        var editor = new ProjectEditor(project);
        editor.Execute(new UsePitchGestureNoteSketchCommand(asset.Id));
        var accepted = Assert.Single(editor.Project.NoteEvents);
        Assert.Equal(192, accepted.StartTick);

        editor.Execute(new SetVocalTakePlacementCommand(asset.Id, new MusicalPosition(9, 1, 0)));

        Assert.Equal(accepted.Id, Assert.Single(editor.Project.NoteEvents).Id);
        Assert.Equal(192, accepted.StartTick);
        Assert.Equal(15_552, Assert.Single(PitchGestureNoteSketcher.Project(project, asset.Id).Events).StartTick);
    }

    private static ProjectAsset CreateAsset()
    {
        var content = Encoding.UTF8.GetBytes("artist-pitched source performance");
        return new ProjectAsset(
            ProjectAssetId.New(),
            ProjectAssetKind.OriginalVocalTake,
            "audio/webm",
            content.LongLength,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            "Pitched take");
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
