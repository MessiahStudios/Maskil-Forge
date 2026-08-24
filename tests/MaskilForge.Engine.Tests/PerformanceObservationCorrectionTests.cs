using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MaskilForge.Domain;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class PerformanceObservationCorrectionTests
{
    [Fact]
    public void ArtistCorrection_IsReversibleAndKeepsAStableIdentity()
    {
        var project = SongProject.Create("Corrected evidence");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var firstUtc = new DateTimeOffset(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);
        var revisedUtc = firstUtc.AddMinutes(3);
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Inaccurate, firstUtc);

        var first = project.SetPerformanceObservationCorrection(
            observation.Id, [new PerformanceMeasurement("frequencyHertz", 220m, "hertz")], firstUtc);
        var revised = project.SetPerformanceObservationCorrection(
            observation.Id, [new PerformanceMeasurement("frequencyHertz", 330m, "hertz")], revisedUtc);

        Assert.Equal(first.Id, revised.Id);
        Assert.Equal(firstUtc, revised.CreatedUtc);
        Assert.Equal(revisedUtc, revised.UpdatedUtc);
        Assert.Equal(330m, Assert.Single(Assert.Single(project.PerformanceObservationCorrections).Measurements).Value);
        Assert.Equal(440m, Assert.Single(Assert.Single(project.PerformanceObservations).Measurements).Value);

        var cleared = project.ClearPerformanceObservationCorrection(observation.Id);

        Assert.Equal(revised, cleared);
        Assert.Empty(project.PerformanceObservationCorrections);
        Assert.Single(project.PerformanceObservationReviews);
        Assert.Equal(440m, Assert.Single(Assert.Single(project.PerformanceObservations).Measurements).Value);
    }

    [Fact]
    public void ArtistCorrection_RequiresAnInaccurateClaimAndAGenuineValueChange()
    {
        var project = SongProject.Create("Correction boundary");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);

        Assert.Throws<InvalidOperationException>(() => project.SetPerformanceObservationCorrection(
            observation.Id, [new PerformanceMeasurement("frequencyHertz", 220m, "hertz")], now));

        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        Assert.Throws<InvalidOperationException>(() => project.SetPerformanceObservationCorrection(
            observation.Id, [new PerformanceMeasurement("frequencyHertz", 220m, "hertz")], now));

        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Inaccurate, now);
        Assert.Throws<ArgumentException>(() => project.SetPerformanceObservationCorrection(
            observation.Id, [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")], now));
        Assert.Throws<ArgumentException>(() => project.SetPerformanceObservationCorrection(
            observation.Id, [new PerformanceMeasurement("frequencyHertz", 220m, "normalized")], now));
        Assert.Throws<ArgumentException>(() => project.SetPerformanceObservationCorrection(
            observation.Id,
            [
                new PerformanceMeasurement("frequencyHertz", 220m, "hertz"),
                new PerformanceMeasurement("strength", 0.5m, "normalized")
            ],
            now));
        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetPerformanceObservationCorrection(
            observation.Id, [new PerformanceMeasurement("frequencyHertz", 40m, "hertz")], now));
        Assert.Throws<KeyNotFoundException>(() => project.SetPerformanceObservationCorrection(
            PerformanceObservationId.New(), [new PerformanceMeasurement("frequencyHertz", 220m, "hertz")], now));
        Assert.Throws<KeyNotFoundException>(() => project.ClearPerformanceObservationCorrection(observation.Id));
    }

    [Fact]
    public void AccurateOrClearedVerdict_DropsAnyStoredCorrection()
    {
        var project = SongProject.Create("Correction invalidation");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "loudness.frame", "maskil.browser.loudness",
            [
                new PerformanceMeasurement("rmsDbfs", -18.2m, "dBFS"),
                new PerformanceMeasurement("peakDbfs", -4.1m, "dBFS")
            ]);
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

        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);

        Assert.Empty(project.PerformanceObservationCorrections);
        Assert.Equal(PerformanceObservationReviewVerdict.Accurate, Assert.Single(project.PerformanceObservationReviews).Verdict);

        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Inaccurate, now);
        project.SetPerformanceObservationCorrection(
            observation.Id,
            [
                new PerformanceMeasurement("rmsDbfs", -10m, "dBFS"),
                new PerformanceMeasurement("peakDbfs", -3m, "dBFS")
            ],
            now);
        project.ClearPerformanceObservationReview(observation.Id);

        Assert.Empty(project.PerformanceObservationReviews);
        Assert.Empty(project.PerformanceObservationCorrections);
    }

    [Fact]
    public void AnalyzerRerun_ClearsOnlyCorrectionsForClaimsItReplaces()
    {
        var project = SongProject.Create("Scoped correction invalidation");
        var asset = CreateAsset();
        var pitch = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var onset = CreateObservation(asset.Id, "onset.event", "maskil.browser.onset-energy",
            [new PerformanceMeasurement("strength", 0.71m, "normalized")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(pitch);
        project.RegisterPerformanceObservation(onset);
        project.SetPerformanceObservationReview(pitch.Id, PerformanceObservationReviewVerdict.Inaccurate, now);
        project.SetPerformanceObservationReview(onset.Id, PerformanceObservationReviewVerdict.Inaccurate, now);
        project.SetPerformanceObservationCorrection(pitch.Id, [new PerformanceMeasurement("frequencyHertz", 220m, "hertz")], now);
        project.SetPerformanceObservationCorrection(onset.Id, [new PerformanceMeasurement("strength", 0.4m, "normalized")], now);
        var replacement = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 330m, "hertz")]);

        project.ReplacePerformanceObservations(
            asset.Id,
            "maskil.browser.pitch-acf",
            "pitch.frame",
            [replacement]);

        Assert.DoesNotContain(project.PerformanceObservations, item => item.Id == pitch.Id);
        var retained = Assert.Single(project.PerformanceObservationCorrections);
        Assert.Equal(onset.Id, retained.ObservationId);
        Assert.Equal(0.4m, Assert.Single(retained.Measurements).Value);
        Assert.Equal(PerformanceObservationReviewVerdict.Inaccurate,
            Assert.Single(project.PerformanceObservationReviews).Verdict);
    }

    [Fact]
    public void SourceTakeRemoval_CascadesAnalyzerClaimsReviewsAndCorrections()
    {
        var project = SongProject.Create("Corrected source cleanup");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Inaccurate, now);
        project.SetPerformanceObservationCorrection(
            observation.Id, [new PerformanceMeasurement("frequencyHertz", 196m, "hertz")], now);

        project.RemoveAsset(asset.Id);

        Assert.Empty(project.Assets);
        Assert.Empty(project.PerformanceObservations);
        Assert.Empty(project.PerformanceObservationReviews);
        Assert.Empty(project.PerformanceObservationCorrections);
    }

    [Fact]
    public void Correction_RoundTripsWithTheProjectWithoutChangingAnalyzerEvidence()
    {
        var project = SongProject.Create("Portable correction");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf",
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(
            observation.Id,
            PerformanceObservationReviewVerdict.Inaccurate,
            new DateTimeOffset(2026, 8, 23, 18, 30, 0, TimeSpan.Zero));
        var correction = project.SetPerformanceObservationCorrection(
            observation.Id,
            [new PerformanceMeasurement("frequencyHertz", 196.2m, "hertz")],
            new DateTimeOffset(2026, 8, 23, 18, 31, 0, TimeSpan.Zero));

        var package = PortableProjectPackage.Export(project, new Dictionary<ProjectAssetId, byte[]>
        {
            [asset.Id] = Encoding.UTF8.GetBytes("artist-corrected source performance")
        });
        var inspected = PortableProjectPackage.Inspect(package);

        var restoredObservation = Assert.Single(inspected.Project.PerformanceObservations);
        Assert.Equal(observation.Id, restoredObservation.Id);
        Assert.Equal(observation.SourceAssetId, restoredObservation.SourceAssetId);
        Assert.Equal(observation.Kind, restoredObservation.Kind);
        Assert.Equal(440m, Assert.Single(restoredObservation.Measurements).Value);
        Assert.Equal(PerformanceObservationReviewVerdict.Inaccurate,
            Assert.Single(inspected.Project.PerformanceObservationReviews).Verdict);
        var restoredCorrection = Assert.Single(inspected.Project.PerformanceObservationCorrections);
        Assert.Equal(correction.Id, restoredCorrection.Id);
        Assert.Equal(correction.ObservationId, restoredCorrection.ObservationId);
        Assert.Equal(correction.CreatedUtc, restoredCorrection.CreatedUtc);
        Assert.Equal(correction.UpdatedUtc, restoredCorrection.UpdatedUtc);
        Assert.Equal(correction.Measurements, restoredCorrection.Measurements);
    }

    [Fact]
    public void Schema25_MigratesToAnExplicitEmptyArtistCorrectionCollection()
    {
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(SongProject.Create("Pre-correction song")))!.AsObject();
        document["schemaVersion"] = 25;
        document.Remove("performanceObservationCorrections");

        var inspected = PortableProjectImporter.Inspect(document.ToJsonString());

        Assert.Equal(25, inspected.SourceSchemaVersion);
        Assert.Equal(SchemaVersion.Current.Value, inspected.Project.SchemaVersion.Value);
        Assert.Empty(inspected.Project.PerformanceObservationCorrections);
        Assert.Empty(inspected.Project.PerformanceObservationReviews);
    }

    private static ProjectAsset CreateAsset()
    {
        var content = Encoding.UTF8.GetBytes("artist-corrected source performance");
        return new ProjectAsset(
            ProjectAssetId.New(),
            ProjectAssetKind.OriginalVocalTake,
            "audio/webm",
            content.LongLength,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            "Corrected take");
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
