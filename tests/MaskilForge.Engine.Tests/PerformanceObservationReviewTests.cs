using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MaskilForge.Domain;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class PerformanceObservationReviewTests
{
    [Fact]
    public void ArtistReview_IsReversibleAndKeepsAStableIdentity()
    {
        var project = SongProject.Create("Reviewed evidence");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf");
        var firstReviewUtc = new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
        var revisedUtc = firstReviewUtc.AddMinutes(2);
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);

        var accurate = project.SetPerformanceObservationReview(
            observation.Id, PerformanceObservationReviewVerdict.Accurate, firstReviewUtc);
        var inaccurate = project.SetPerformanceObservationReview(
            observation.Id, PerformanceObservationReviewVerdict.Inaccurate, revisedUtc);

        Assert.Equal(accurate.Id, inaccurate.Id);
        Assert.Equal(firstReviewUtc, inaccurate.CreatedUtc);
        Assert.Equal(revisedUtc, inaccurate.UpdatedUtc);
        Assert.Equal(PerformanceObservationReviewVerdict.Inaccurate, Assert.Single(project.PerformanceObservationReviews).Verdict);

        var cleared = project.ClearPerformanceObservationReview(observation.Id);

        Assert.Equal(inaccurate, cleared);
        Assert.Empty(project.PerformanceObservationReviews);
        Assert.Single(project.PerformanceObservations);
    }

    [Fact]
    public void ArtistReview_RequiresAnExistingObservationAndOneReviewPerClaim()
    {
        var project = SongProject.Create("Review boundary");
        var reviewedUtc = DateTimeOffset.UtcNow;

        Assert.Throws<KeyNotFoundException>(() => project.SetPerformanceObservationReview(
            PerformanceObservationId.New(), PerformanceObservationReviewVerdict.Accurate, reviewedUtc));
        Assert.Throws<KeyNotFoundException>(() => project.ClearPerformanceObservationReview(PerformanceObservationId.New()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PerformanceObservationReview(
            PerformanceObservationReviewId.New(),
            PerformanceObservationId.New(),
            PerformanceObservationReviewVerdict.Accurate,
            reviewedUtc,
            reviewedUtc.AddSeconds(-1)));
    }

    [Fact]
    public void AnalyzerRerun_ClearsOnlyReviewsForClaimsItReplaces()
    {
        var project = SongProject.Create("Scoped review invalidation");
        var asset = CreateAsset();
        var pitch = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf");
        var onset = CreateObservation(asset.Id, "onset.event", "maskil.browser.onset-energy");
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(pitch);
        project.RegisterPerformanceObservation(onset);
        project.SetPerformanceObservationReview(pitch.Id, PerformanceObservationReviewVerdict.Accurate, DateTimeOffset.UtcNow);
        project.SetPerformanceObservationReview(onset.Id, PerformanceObservationReviewVerdict.Inaccurate, DateTimeOffset.UtcNow);
        var replacement = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf");

        project.ReplacePerformanceObservations(
            asset.Id,
            "maskil.browser.pitch-acf",
            "pitch.frame",
            [replacement]);

        Assert.DoesNotContain(project.PerformanceObservations, item => item.Id == pitch.Id);
        Assert.Contains(project.PerformanceObservations, item => item.Id == replacement.Id);
        var retainedReview = Assert.Single(project.PerformanceObservationReviews);
        Assert.Equal(onset.Id, retainedReview.ObservationId);
        Assert.Equal(PerformanceObservationReviewVerdict.Inaccurate, retainedReview.Verdict);
    }

    [Fact]
    public void SourceTakeRemoval_CascadesAnalyzerClaimsAndArtistReviews()
    {
        var project = SongProject.Create("Reviewed source cleanup");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf");
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(
            observation.Id,
            PerformanceObservationReviewVerdict.Accurate,
            DateTimeOffset.UtcNow);

        project.RemoveAsset(asset.Id);

        Assert.Empty(project.Assets);
        Assert.Empty(project.PerformanceObservations);
        Assert.Empty(project.PerformanceObservationReviews);
    }

    [Fact]
    public void Review_RoundTripsWithTheProjectWithoutChangingAnalyzerEvidence()
    {
        var project = SongProject.Create("Portable review");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf");
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        var review = project.SetPerformanceObservationReview(
            observation.Id,
            PerformanceObservationReviewVerdict.Accurate,
            new DateTimeOffset(2026, 8, 22, 18, 30, 0, TimeSpan.Zero));

        var package = PortableProjectPackage.Export(project, new Dictionary<ProjectAssetId, byte[]>
        {
            [asset.Id] = Encoding.UTF8.GetBytes("artist-reviewed source performance")
        });
        var inspected = PortableProjectPackage.Inspect(package);

        var restoredObservation = Assert.Single(inspected.Project.PerformanceObservations);
        Assert.Equal(observation.Id, restoredObservation.Id);
        Assert.Equal(observation.SourceAssetId, restoredObservation.SourceAssetId);
        Assert.Equal(observation.Kind, restoredObservation.Kind);
        Assert.Equal(Assert.Single(observation.Measurements), Assert.Single(restoredObservation.Measurements));
        Assert.Equal(review, Assert.Single(inspected.Project.PerformanceObservationReviews));
    }

    [Fact]
    public void Schema24_MigratesToAnExplicitEmptyArtistReviewCollection()
    {
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(SongProject.Create("Pre-review song")))!.AsObject();
        document["schemaVersion"] = 24;
        document.Remove("performanceObservationReviews");

        var inspected = PortableProjectImporter.Inspect(document.ToJsonString());

        Assert.Equal(24, inspected.SourceSchemaVersion);
        Assert.Equal(26, inspected.Project.SchemaVersion.Value);
        Assert.Empty(inspected.Project.PerformanceObservationReviews);
    }

    private static ProjectAsset CreateAsset()
    {
        var content = Encoding.UTF8.GetBytes("artist-reviewed source performance");
        return new ProjectAsset(
            ProjectAssetId.New(),
            ProjectAssetKind.OriginalVocalTake,
            "audio/webm",
            content.LongLength,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            "Reviewed take");
    }

    private static PerformanceObservation CreateObservation(ProjectAssetId assetId, string kind, string analyzerId) => new(
        PerformanceObservationId.New(),
        assetId,
        kind,
        200,
        80,
        [new PerformanceMeasurement("value", 0.75m, "normalized")],
        0.8m,
        analyzerId,
        "1.0.0",
        PerformanceObservationProvenance.DeterministicAnalyzer,
        DateTimeOffset.UtcNow);
}
