using System.Text;
using MaskilForge.Api;
using MaskilForge.Domain;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class LoudnessObservationTests
{
    [Fact]
    public void ReportBoundary_StampsAnalyzerIdentityAndRejectsMalformedFrames()
    {
        var sourceAssetId = ProjectAssetId.New();
        var createdUtc = new DateTimeOffset(2026, 8, 22, 3, 0, 0, TimeSpan.Zero);
        var observations = LoudnessObservationReport.CreateObservations(sourceAssetId, ValidFrames(), createdUtc);

        Assert.Equal(2, observations.Count);
        Assert.All(observations, observation =>
        {
            Assert.Equal(sourceAssetId, observation.SourceAssetId);
            Assert.Equal(LoudnessObservationReport.ObservationKind, observation.Kind);
            Assert.Equal(LoudnessObservationReport.AnalyzerId, observation.AnalyzerId);
            Assert.Equal(LoudnessObservationReport.AnalyzerVersion, observation.AnalyzerVersion);
            Assert.Equal(PerformanceObservationProvenance.DeterministicAnalyzer, observation.Provenance);
            Assert.Null(observation.Confidence);
            Assert.Equal(createdUtc, observation.CreatedUtc);
            Assert.Equal(["rmsDbfs", "peakDbfs"], observation.Measurements.Select(item => item.Name));
            Assert.All(observation.Measurements, measurement => Assert.Equal("dBFS", measurement.Unit));
        });

        Assert.Throws<ArgumentException>(() => LoudnessObservationReport.CreateObservations(sourceAssetId, [], createdUtc));
        Assert.Throws<ArgumentException>(() => LoudnessObservationReport.CreateObservations(sourceAssetId,
            [new LoudnessFrameReport(1, 250, -20, -5)], createdUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoudnessObservationReport.CreateObservations(sourceAssetId,
            [new LoudnessFrameReport(0, 125, -20, -5), new LoudnessFrameReport(125, 125, -20, -5)], createdUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoudnessObservationReport.CreateObservations(sourceAssetId,
            [new LoudnessFrameReport(0, 250, -121, -5)], createdUtc));
        Assert.Throws<ArgumentException>(() => LoudnessObservationReport.CreateObservations(sourceAssetId,
            [new LoudnessFrameReport(0, 250, -3, -6)], createdUtc));
    }

    [Fact]
    public async Task Workspace_RerunReplacesOnlyLoudnessFramesAndNeverChangesSourceBytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-loudness-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Measured take", CancellationToken.None);
            var content = Encoding.UTF8.GetBytes("artist source bytes stay immutable");
            var saved = await workspace.AddOriginalVocalTakeAsync(
                editor.Project.Id,
                editor.Project.LastModifiedUtc,
                "audio/webm;codecs=opus",
                content,
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            var asset = Assert.Single(saved!.Project.Assets);
            var pitchObservation = new PerformanceObservation(
                PerformanceObservationId.New(),
                asset.Id,
                "pitch.frame",
                0,
                20,
                [new PerformanceMeasurement("frequency", 440, "hertz")],
                .9m,
                "maskil.pitch-frame",
                "1.0.0",
                PerformanceObservationProvenance.DeterministicAnalyzer,
                DateTimeOffset.UtcNow);
            saved.Project.RegisterPerformanceObservation(pitchObservation);
            await repository.SaveAsync(saved.Project);

            var first = await workspace.ReplaceLoudnessObservationsAsync(
                saved.Project.Id,
                asset.Id,
                saved.Project.LastModifiedUtc,
                ValidFrames(),
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            Assert.NotNull(first);
            var firstLoudnessIds = first.Project.PerformanceObservations
                .Where(item => item.AnalyzerId == LoudnessObservationReport.AnalyzerId)
                .Select(item => item.Id)
                .ToHashSet();
            Assert.Equal(2, firstLoudnessIds.Count);
            Assert.Contains(first.Project.PerformanceObservations, item => item.Id == pitchObservation.Id);

            var second = await workspace.ReplaceLoudnessObservationsAsync(
                saved.Project.Id,
                asset.Id,
                first.Project.LastModifiedUtc,
                [new LoudnessFrameReport(0, 250, -18.5m, -4.5m)],
                DateTimeOffset.UtcNow,
                CancellationToken.None);

            Assert.NotNull(second);
            var secondLoudness = Assert.Single(second.Project.PerformanceObservations, item => item.AnalyzerId == LoudnessObservationReport.AnalyzerId);
            Assert.DoesNotContain(secondLoudness.Id, firstLoudnessIds);
            Assert.Contains(second.Project.PerformanceObservations, item => item.Id == pitchObservation.Id);
            await using var stored = await repository.OpenAssetAsync(saved.Project.Id, asset.Id);
            using var copied = new MemoryStream();
            await stored!.CopyToAsync(copied);
            Assert.Equal(content, copied.ToArray());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Workspace_RejectsStaleOrMissingTakeWithoutPersistingEvidence()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-loudness-stale-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Stale analysis", CancellationToken.None);

            await Assert.ThrowsAsync<StaleProjectSessionException>(() => workspace.ReplaceLoudnessObservationsAsync(
                editor.Project.Id,
                ProjectAssetId.New(),
                editor.Project.LastModifiedUtc.AddSeconds(-1),
                ValidFrames(),
                DateTimeOffset.UtcNow,
                CancellationToken.None));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => workspace.ReplaceLoudnessObservationsAsync(
                editor.Project.Id,
                ProjectAssetId.New(),
                editor.Project.LastModifiedUtc,
                ValidFrames(),
                DateTimeOffset.UtcNow,
                CancellationToken.None));

            var persisted = await repository.LoadAsync(editor.Project.Id);
            Assert.Empty(persisted!.PerformanceObservations);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static IReadOnlyList<LoudnessFrameReport> ValidFrames() =>
    [
        new LoudnessFrameReport(0, 250, -24.5m, -6.25m),
        new LoudnessFrameReport(250, 125, -30m, -8m)
    ];
}
