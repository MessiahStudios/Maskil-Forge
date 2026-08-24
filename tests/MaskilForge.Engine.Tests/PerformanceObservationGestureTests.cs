using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MaskilForge.Domain;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class PerformanceObservationGestureTests
{
    [Fact]
    public void ArtistGesture_IsReversibleAndKeepsAStableIdentity()
    {
        var project = SongProject.Create("Gestured evidence");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var firstUtc = new DateTimeOffset(2026, 8, 23, 19, 0, 0, TimeSpan.Zero);
        var revisedUtc = firstUtc.AddMinutes(4);
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, firstUtc);

        var first = project.SetPerformanceObservationGesture(observation.Id, firstUtc);
        var revised = project.SetPerformanceObservationGesture(observation.Id, revisedUtc);

        Assert.Equal(first.Id, revised.Id);
        Assert.Equal(firstUtc, revised.CreatedUtc);
        Assert.Equal(revisedUtc, revised.UpdatedUtc);
        Assert.Equal(440m, Assert.Single(Assert.Single(project.PerformanceObservationGestures).Measurements).Value);
        Assert.Equal(440m, Assert.Single(Assert.Single(project.PerformanceObservations).Measurements).Value);

        var cleared = project.ClearPerformanceObservationGesture(observation.Id);

        Assert.Equal(revised, cleared);
        Assert.Empty(project.PerformanceObservationGestures);
        Assert.Single(project.PerformanceObservationReviews);
        Assert.Single(project.PerformanceObservations);
    }

    [Fact]
    public void ArtistGesture_RequiresAnAccurateClaimOrACorrectedInaccurateClaim()
    {
        var project = SongProject.Create("Gesture boundary");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);

        Assert.Throws<InvalidOperationException>(() => project.SetPerformanceObservationGesture(observation.Id, now));

        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Inaccurate, now);
        Assert.Throws<InvalidOperationException>(() => project.SetPerformanceObservationGesture(observation.Id, now));

        project.SetPerformanceObservationCorrection(
            observation.Id, [new PerformanceMeasurement("frequencyHertz", 220m, "hertz")], now);
        var gestured = project.SetPerformanceObservationGesture(observation.Id, now);
        Assert.Equal(220m, Assert.Single(gestured.Measurements).Value);

        Assert.Throws<KeyNotFoundException>(() => project.SetPerformanceObservationGesture(PerformanceObservationId.New(), now));
        Assert.Throws<KeyNotFoundException>(() => project.ClearPerformanceObservationGesture(PerformanceObservationId.New()));
    }

    [Fact]
    public void UnreviewedOrUncorrectedInaccurate_DropsAnyStoredGesture()
    {
        var project = SongProject.Create("Gesture invalidation");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "loudness.frame", "maskil.browser.loudness",
            [
                new PerformanceMeasurement("rmsDbfs", -18.2m, "dBFS"),
                new PerformanceMeasurement("peakDbfs", -4.1m, "dBFS")
            ]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);

        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Inaccurate, now);

        Assert.Empty(project.PerformanceObservationGestures);
        Assert.Equal(PerformanceObservationReviewVerdict.Inaccurate, Assert.Single(project.PerformanceObservationReviews).Verdict);

        project.SetPerformanceObservationCorrection(
            observation.Id,
            [
                new PerformanceMeasurement("rmsDbfs", -12m, "dBFS"),
                new PerformanceMeasurement("peakDbfs", -4.1m, "dBFS")
            ],
            now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        project.ClearPerformanceObservationCorrection(observation.Id);

        Assert.Empty(project.PerformanceObservationGestures);
        project.SetPerformanceObservationCorrection(
            observation.Id,
            [
                new PerformanceMeasurement("rmsDbfs", -10m, "dBFS"),
                new PerformanceMeasurement("peakDbfs", -3m, "dBFS")
            ],
            now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        project.ClearPerformanceObservationReview(observation.Id);

        Assert.Empty(project.PerformanceObservationReviews);
        Assert.Empty(project.PerformanceObservationCorrections);
        Assert.Empty(project.PerformanceObservationGestures);
    }

    [Fact]
    public void AccuratePromotion_CopiesAnalyzerValuesWithoutChangingEvidence()
    {
        var project = SongProject.Create("Accurate gesture snapshot");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "onset.event", "maskil.browser.onset-energy",
            [new PerformanceMeasurement("strength", 0.71m, "normalized")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);

        project.SetPerformanceObservationGesture(observation.Id, now);

        Assert.Equal(0.71m, Assert.Single(Assert.Single(project.PerformanceObservationGestures).Measurements).Value);
        Assert.Equal(0.71m, Assert.Single(Assert.Single(project.PerformanceObservations).Measurements).Value);
    }

    [Fact]
    public void AnalyzerRerun_ClearsOnlyGesturesForClaimsItReplaces()
    {
        var project = SongProject.Create("Scoped gesture invalidation");
        var asset = CreateAsset();
        var pitch = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var onset = CreateObservation(asset.Id, "onset.event", "maskil.browser.onset-energy",
            [new PerformanceMeasurement("strength", 0.71m, "normalized")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(pitch);
        project.RegisterPerformanceObservation(onset);
        project.SetPerformanceObservationReview(pitch.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationReview(onset.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(pitch.Id, now);
        project.SetPerformanceObservationGesture(onset.Id, now);
        var replacement = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 330m, "hertz")]);

        project.ReplacePerformanceObservations(
            asset.Id,
            "maskil.browser.pitch-acf",
            "pitch.frame",
            [replacement]);

        Assert.DoesNotContain(project.PerformanceObservations, item => item.Id == pitch.Id);
        var retained = Assert.Single(project.PerformanceObservationGestures);
        Assert.Equal(onset.Id, retained.ObservationId);
        Assert.Equal(0.71m, Assert.Single(retained.Measurements).Value);
    }

    [Fact]
    public void SourceTakeRemoval_CascadesAnalyzerClaimsReviewsCorrectionsAndGestures()
    {
        var project = SongProject.Create("Gestured source cleanup");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);

        project.RemoveAsset(asset.Id);

        Assert.Empty(project.Assets);
        Assert.Empty(project.PerformanceObservations);
        Assert.Empty(project.PerformanceObservationReviews);
        Assert.Empty(project.PerformanceObservationCorrections);
        Assert.Empty(project.PerformanceObservationGestures);
    }

    [Fact]
    public void Gesture_RoundTripsWithTheProjectWithoutChangingAnalyzerEvidence()
    {
        var project = SongProject.Create("Portable gesture");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(
            observation.Id,
            PerformanceObservationReviewVerdict.Inaccurate,
            new DateTimeOffset(2026, 8, 23, 19, 30, 0, TimeSpan.Zero));
        project.SetPerformanceObservationCorrection(
            observation.Id,
            [new PerformanceMeasurement("frequencyHertz", 196.2m, "hertz")],
            new DateTimeOffset(2026, 8, 23, 19, 31, 0, TimeSpan.Zero));
        var gesture = project.SetPerformanceObservationGesture(
            observation.Id,
            new DateTimeOffset(2026, 8, 23, 19, 32, 0, TimeSpan.Zero));

        var package = PortableProjectPackage.Export(project, new Dictionary<ProjectAssetId, byte[]>
        {
            [asset.Id] = Encoding.UTF8.GetBytes("artist-gestured source performance")
        });
        var inspected = PortableProjectPackage.Inspect(package);

        var restoredObservation = Assert.Single(inspected.Project.PerformanceObservations);
        Assert.Equal(observation.Id, restoredObservation.Id);
        Assert.Equal(440m, Assert.Single(restoredObservation.Measurements).Value);
        Assert.Equal(196.2m, Assert.Single(Assert.Single(inspected.Project.PerformanceObservationCorrections).Measurements).Value);
        var restoredGesture = Assert.Single(inspected.Project.PerformanceObservationGestures);
        Assert.Equal(gesture.Id, restoredGesture.Id);
        Assert.Equal(gesture.ObservationId, restoredGesture.ObservationId);
        Assert.Equal(gesture.CreatedUtc, restoredGesture.CreatedUtc);
        Assert.Equal(gesture.UpdatedUtc, restoredGesture.UpdatedUtc);
        Assert.Equal(gesture.Measurements, restoredGesture.Measurements);
    }

    [Fact]
    public void Schema26_MigratesToAnExplicitEmptyArtistGestureCollection()
    {
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(SongProject.Create("Pre-gesture song")))!.AsObject();
        document["schemaVersion"] = 26;
        document.Remove("performanceObservationGestures");

        var inspected = PortableProjectImporter.Inspect(document.ToJsonString());

        Assert.Equal(26, inspected.SourceSchemaVersion);
        Assert.Equal(27, inspected.Project.SchemaVersion.Value);
        Assert.Empty(inspected.Project.PerformanceObservationGestures);
        Assert.Empty(inspected.Project.PerformanceObservationCorrections);
    }

    private static ProjectAsset CreateAsset()
    {
        var content = Encoding.UTF8.GetBytes("artist-gestured source performance");
        return new ProjectAsset(
            ProjectAssetId.New(),
            ProjectAssetKind.OriginalVocalTake,
            "audio/webm",
            content.LongLength,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            "Gestured take");
    }

    private static PerformanceObservation CreateObservation(
        ProjectAssetId assetId,
        string kind,
        string analyzerId,
        IReadOnlyList<PerformanceMeasurement> measurements) => new(
        PerformanceObservationId.New(),
        assetId,
        kind,
        200,
        80,
        measurements,
        0.8m,
        analyzerId,
        "1.0.0",
        PerformanceObservationProvenance.DeterministicAnalyzer,
        DateTimeOffset.UtcNow);
}
