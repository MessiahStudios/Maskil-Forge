using System.Security.Cryptography;
using System.Text;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class VocalMelodyCheckTests
{
    [Fact]
    public void Check_ComparesSungMomentsWithWrittenNotesAndLeavesTheTake()
    {
        var project = SongProject.Create("Melody check");
        var source = Encoding.UTF8.GetBytes("immutable human performance");
        var asset = new ProjectAsset(ProjectAssetId.New(), ProjectAssetKind.OriginalVocalTake, "audio/ogg", source.Length,
            Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant(), DateTimeOffset.UtcNow, "Take 1");
        project.RegisterAsset(asset);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.A, Accidental.Natural, 4), 0, 480, 90);
        project.RegisterPerformanceObservation(Frame(asset.Id, 0, 440m));
        project.RegisterPerformanceObservation(Frame(asset.Id, 200, 220m));
        project.RegisterPerformanceObservation(Frame(asset.Id, 400, 493.88m));
        project.RegisterPerformanceObservation(Frame(asset.Id, 800, 440m));
        var revision = project.LastModifiedUtc;

        var check = VocalMelodyChecker.Check(project, asset.Id);

        Assert.Equal(4, check.SungMoments);
        Assert.Equal(2, check.WithMelody);
        Assert.Equal(1, check.AboveMelody);
        Assert.Equal(0, check.BelowMelody);
        Assert.Equal(1, check.Unaligned);
        Assert.Equal(VocalMelodyRelation.Above, Assert.Single(check.Examples).Relation);
        Assert.Equal(400, check.Examples[0].StartMilliseconds);
        Assert.Equal(revision, project.LastModifiedUtc);
        Assert.Equal(asset.Sha256, Assert.Single(project.Assets).Sha256);
        Assert.False(Assert.Single(check.Examples).ArtistCorrected);
        Assert.Equal(0, check.CorrectedMoments);
    }

    [Fact]
    public void Check_UsesAStoredPitchCorrectionAndKeepsTheOriginalFrame()
    {
        var project = SongProject.Create("Melody check");
        var source = Encoding.UTF8.GetBytes("immutable human performance");
        var asset = new ProjectAsset(ProjectAssetId.New(), ProjectAssetKind.OriginalVocalTake, "audio/ogg", source.Length,
            Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant(), DateTimeOffset.UtcNow, "Take 1");
        project.RegisterAsset(asset);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.A, Accidental.Natural, 4), 0, 480, 90);
        var mistaken = Frame(asset.Id, 0, 493.88m);
        project.RegisterPerformanceObservation(mistaken);
        var reviewedAt = DateTimeOffset.UtcNow;
        project.SetPerformanceObservationReview(mistaken.Id, PerformanceObservationReviewVerdict.Inaccurate, reviewedAt);
        project.SetPerformanceObservationCorrection(mistaken.Id, [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")], reviewedAt);
        var revision = project.LastModifiedUtc;

        var check = VocalMelodyChecker.Check(project, asset.Id);

        Assert.Equal(1, check.WithMelody);
        Assert.Equal(0, check.AboveMelody);
        Assert.Equal(1, check.CorrectedMoments);
        Assert.True(Assert.Single(check.Examples).ArtistCorrected);
        Assert.Equal(69, Assert.Single(check.Examples).SungMidi);
        Assert.Contains("stored pitch correction", check.Summary);
        Assert.Equal(493.88m, project.PerformanceObservations.Single().Measurements.Single().Value);
        Assert.Equal(revision, project.LastModifiedUtc);
    }

    [Fact]
    public void Check_ExplainsAMissingMelodyAndMissingPitch()
    {
        var project = SongProject.Create("Melody check");
        var source = Encoding.UTF8.GetBytes("immutable human performance");
        var asset = new ProjectAsset(ProjectAssetId.New(), ProjectAssetKind.OriginalVocalTake, "audio/ogg", source.Length,
            Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant(), DateTimeOffset.UtcNow, "Take 1");
        project.RegisterAsset(asset);
        var missingPitch = VocalMelodyChecker.Check(project, asset.Id);
        Assert.Contains("Analyze pitch", missingPitch.Summary);
        project.RegisterPerformanceObservation(Frame(asset.Id, 0, 440m));
        var missingNotes = VocalMelodyChecker.Check(project, asset.Id);
        Assert.Contains("Write the notes", missingNotes.Summary);
        Assert.Empty(missingNotes.Examples);
    }

    private static PerformanceObservation Frame(ProjectAssetId assetId, long start, decimal hertz) =>
        new(PerformanceObservationId.New(), assetId, VocalMelodyChecker.ObservationKind, start, 80,
            [new PerformanceMeasurement("frequencyHertz", hertz, "hertz")], 0.9m,
            VocalMelodyChecker.AnalyzerId, VocalMelodyChecker.AnalyzerVersion,
            PerformanceObservationProvenance.DeterministicAnalyzer, DateTimeOffset.UtcNow);
}
