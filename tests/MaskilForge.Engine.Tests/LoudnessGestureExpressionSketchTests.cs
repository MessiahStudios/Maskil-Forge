using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class LoudnessGestureExpressionSketchTests
{
    [Fact]
    public void Project_MapsApprovedRmsToDynamicsPointsAndTakeRelativeTicks()
    {
        var project = SongProject.Create("Loudness expression");
        var asset = CreateAsset();
        var observation = CreateLoudness(asset.Id, 0, 250, -18.2m, -4.1m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);

        var sketch = LoudnessGestureExpressionSketcher.Project(project, asset.Id);
        var point = Assert.Single(sketch.Points);

        Assert.Equal(asset.Id, sketch.SourceAssetId);
        Assert.Equal("Loudness take dynamics", sketch.Name);
        Assert.Equal(ExpressionCurveKind.Dynamics, sketch.Kind);
        Assert.Equal(0, sketch.StartTick);
        Assert.Equal(0, point.Tick);
        Assert.Equal(88, point.Value);
    }

    [Fact]
    public void Project_UsesCorrectedRmsWhenTheClaimIsInaccurate()
    {
        var project = SongProject.Create("Corrected loudness expression");
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

        var point = Assert.Single(LoudnessGestureExpressionSketcher.Project(project, asset.Id).Points);

        Assert.Equal(102, point.Value);
        Assert.Equal(0, point.Tick);
    }

    [Fact]
    public void Project_UsesFallbackValueWhenRmsIsMissing()
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

        var point = Assert.Single(LoudnessGestureExpressionSketcher.Project(project, asset.Id).Points);

        Assert.Equal(96, point.Value);
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

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => LoudnessGestureExpressionSketcher.Project(project, asset.Id));

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

        var error = Assert.Throws<InvalidOperationException>(() => LoudnessGestureExpressionSketcher.Project(project, asset.Id));

        Assert.Contains("Promote at least one loudness claim", error.Message);
    }

    [Fact]
    public void Project_OffsetsTakeRelativeTicksByTheArtistPlacement()
    {
        var project = SongProject.Create("Placed loudness expression");
        var asset = CreateAsset();
        var observation = CreateLoudness(asset.Id, 0, 250, -18.2m, -4.1m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        project.SetVocalTakePlacement(asset.Id, new MusicalPosition(9, 1, 0));

        var sketch = LoudnessGestureExpressionSketcher.Project(project, asset.Id);
        var point = Assert.Single(sketch.Points);

        Assert.Equal(15_360, sketch.StartTick);
        Assert.Equal(15_360, point.Tick);
        Assert.Equal(88, point.Value);
    }

    [Fact]
    public void UseLoudnessGestureExpressionSketchCommand_IsExplicitAdditiveAndReversible()
    {
        var project = SongProject.Create("Accepted loudness expression");
        var asset = CreateAsset();
        var observation = CreateLoudness(asset.Id, 0, 250, -18.2m, -4.1m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        var editor = new ProjectEditor(project);
        var command = new UseLoudnessGestureExpressionSketchCommand(asset.Id);

        editor.Execute(command);
        var accepted = Assert.Single(editor.Project.ExpressionCurves);
        Assert.Equal("Loudness take dynamics", accepted.Name);
        Assert.Equal(ExpressionCurveKind.Dynamics, accepted.Kind);
        Assert.Equal(0, Assert.Single(accepted.Points).Tick);
        Assert.Equal(88, Assert.Single(accepted.Points).Value);

        editor.Undo();
        Assert.Empty(editor.Project.ExpressionCurves);

        editor.Redo();
        Assert.Equal(accepted.Id, Assert.Single(editor.Project.ExpressionCurves).Id);

        project.ClearPerformanceObservationGesture(observation.Id);
        Assert.Equal(accepted.Id, Assert.Single(editor.Project.ExpressionCurves).Id);
    }

    [Fact]
    public void ChangingPlacement_DoesNotMoveAlreadyAcceptedExpressionPoints()
    {
        var project = SongProject.Create("Stable expression points");
        var asset = CreateAsset();
        var observation = CreateLoudness(asset.Id, 0, 250, -18.2m, -4.1m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        var editor = new ProjectEditor(project);
        editor.Execute(new UseLoudnessGestureExpressionSketchCommand(asset.Id));
        var accepted = Assert.Single(editor.Project.ExpressionCurves);
        Assert.Equal(0, Assert.Single(accepted.Points).Tick);

        editor.Execute(new SetVocalTakePlacementCommand(asset.Id, new MusicalPosition(9, 1, 0)));

        Assert.Equal(accepted.Id, Assert.Single(editor.Project.ExpressionCurves).Id);
        Assert.Equal(0, Assert.Single(accepted.Points).Tick);
        Assert.Equal(15_360, Assert.Single(LoudnessGestureExpressionSketcher.Project(project, asset.Id).Points).Tick);
    }

    [Fact]
    public void RemovingTheTake_DoesNotDropAcceptedExpressionCurves()
    {
        var project = SongProject.Create("Orphaned expression");
        var asset = CreateAsset();
        var observation = CreateLoudness(asset.Id, 0, 250, -18.2m, -4.1m);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        var editor = new ProjectEditor(project);
        editor.Execute(new UseLoudnessGestureExpressionSketchCommand(asset.Id));
        var accepted = Assert.Single(editor.Project.ExpressionCurves);

        project.RemoveAsset(asset.Id);

        Assert.Equal(accepted.Id, Assert.Single(project.ExpressionCurves).Id);
        Assert.Equal(88, Assert.Single(accepted.Points).Value);
    }

    [Fact]
    public void Schema28_MigratesToAnExplicitEmptyExpressionCurveCollection()
    {
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(SongProject.Create("Pre-curve song")))!.AsObject();
        document["schemaVersion"] = 28;
        document.Remove("expressionCurves");

        var inspected = PortableProjectImporter.Inspect(document.ToJsonString());

        Assert.Equal(28, inspected.SourceSchemaVersion);
        Assert.Equal(SchemaVersion.Current.Value, inspected.Project.SchemaVersion.Value);
        Assert.Empty(inspected.Project.ExpressionCurves);
        Assert.Empty(inspected.Project.VocalTakePlacements);
    }

    [Fact]
    public void Curve_RoundTripsWithTheProjectWithoutAttachingAudio()
    {
        var project = SongProject.Create("Portable expression");
        var asset = CreateAsset();
        project.RegisterAsset(asset);
        var curve = project.AddExpressionCurve(
            "Loudness take dynamics",
            ExpressionCurveKind.Dynamics,
            [new ExpressionCurvePoint(192, 88)]);

        var package = PortableProjectPackage.Export(project, new Dictionary<ProjectAssetId, byte[]>
        {
            [asset.Id] = Encoding.UTF8.GetBytes("artist-loudness source performance")
        });
        var inspected = PortableProjectPackage.Inspect(package);

        var restored = Assert.Single(inspected.Project.ExpressionCurves);
        Assert.Equal(curve.Id, restored.Id);
        Assert.Equal(curve.Name, restored.Name);
        Assert.Equal(curve.Kind, restored.Kind);
        Assert.Equal(192, Assert.Single(restored.Points).Tick);
        Assert.Equal(88, Assert.Single(restored.Points).Value);
        Assert.Equal(SchemaVersion.Current.Value, inspected.Project.SchemaVersion.Value);
    }

    [Fact]
    public void Project_RequiresAnExistingOriginalVocalTake()
    {
        var project = SongProject.Create("Missing take");

        Assert.Throws<KeyNotFoundException>(() => LoudnessGestureExpressionSketcher.Project(project, ProjectAssetId.New()));
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
