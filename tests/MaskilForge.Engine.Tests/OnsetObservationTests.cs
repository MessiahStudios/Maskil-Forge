using System.Text;
using MaskilForge.Api;
using MaskilForge.Domain;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class OnsetObservationTests
{
    [Fact]
    public void ReportBoundary_StampsConfidenceAndRejectsInvalidOnsetClaims()
    {
        var sourceAssetId = ProjectAssetId.New();
        var createdUtc = new DateTimeOffset(2026, 8, 22, 7, 0, 0, TimeSpan.Zero);
        var observations = OnsetObservationReport.CreateObservations(sourceAssetId, ValidEvents(), createdUtc);

        Assert.Equal(2, observations.Count);
        Assert.All(observations, observation =>
        {
            Assert.Equal(sourceAssetId, observation.SourceAssetId);
            Assert.Equal(OnsetObservationReport.ObservationKind, observation.Kind);
            Assert.Equal(OnsetObservationReport.AnalyzerId, observation.AnalyzerId);
            Assert.Equal(OnsetObservationReport.AnalyzerVersion, observation.AnalyzerVersion);
            Assert.Equal(PerformanceObservationProvenance.DeterministicAnalyzer, observation.Provenance);
            Assert.InRange(observation.Confidence!.Value, OnsetObservationReport.MinimumConfidence, 1);
            var measurement = Assert.Single(observation.Measurements);
            Assert.Equal("strength", measurement.Name);
            Assert.Equal("normalized", measurement.Unit);
            Assert.InRange(measurement.Value, 0, 1);
            Assert.Equal(createdUtc, observation.CreatedUtc);
        });
        Assert.Empty(OnsetObservationReport.CreateObservations(sourceAssetId, [], createdUtc));

        Assert.Throws<ArgumentException>(() => OnsetObservationReport.CreateObservations(sourceAssetId,
            [new OnsetEventReport(1, 32, .8m, .9m)], createdUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => OnsetObservationReport.CreateObservations(sourceAssetId,
            [new OnsetEventReport(0, 16, .8m, .9m)], createdUtc));
        Assert.Throws<ArgumentException>(() => OnsetObservationReport.CreateObservations(sourceAssetId,
            [new OnsetEventReport(0, 32, .8m, .9m), new OnsetEventReport(80, 32, .8m, .9m)], createdUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => OnsetObservationReport.CreateObservations(sourceAssetId,
            [new OnsetEventReport(0, 32, 1.1m, .9m)], createdUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => OnsetObservationReport.CreateObservations(sourceAssetId,
            [new OnsetEventReport(0, 32, .8m, .5m)], createdUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => OnsetObservationReport.CreateObservations(sourceAssetId,
            [new OnsetEventReport(long.MaxValue - (long.MaxValue % 16), 32, .8m, .9m)], createdUtc));
    }

    [Fact]
    public async Task Workspace_RerunReplacesOrClearsOnlyOnsetEventsWithoutChangingSourceBytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-onset-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Onset evidence", CancellationToken.None);
            var content = Encoding.UTF8.GetBytes("immutable rhythmic source bytes");
            var saved = await workspace.AddOriginalVocalTakeAsync(
                editor.Project.Id,
                editor.Project.LastModifiedUtc,
                "audio/webm;codecs=opus",
                content,
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            var asset = Assert.Single(saved!.Project.Assets);
            var loudnessObservation = Assert.Single(LoudnessObservationReport.CreateObservations(
                asset.Id,
                [new LoudnessFrameReport(0, 250, -24, -6)],
                DateTimeOffset.UtcNow));
            var pitchObservation = Assert.Single(PitchObservationReport.CreateObservations(
                asset.Id,
                [new PitchFrameReport(0, 80, 220, .9m)],
                DateTimeOffset.UtcNow));
            saved.Project.RegisterPerformanceObservation(loudnessObservation);
            saved.Project.RegisterPerformanceObservation(pitchObservation);
            await repository.SaveAsync(saved.Project);

            var first = await workspace.ReplaceOnsetObservationsAsync(
                saved.Project.Id,
                asset.Id,
                saved.Project.LastModifiedUtc,
                ValidEvents(),
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            Assert.NotNull(first);
            var firstOnsetIds = first.Project.PerformanceObservations
                .Where(item => item.AnalyzerId == OnsetObservationReport.AnalyzerId)
                .Select(item => item.Id)
                .ToHashSet();
            Assert.Equal(2, firstOnsetIds.Count);
            Assert.Contains(first.Project.PerformanceObservations, item => item.Id == loudnessObservation.Id);
            Assert.Contains(first.Project.PerformanceObservations, item => item.Id == pitchObservation.Id);

            var rerun = await workspace.ReplaceOnsetObservationsAsync(
                saved.Project.Id,
                asset.Id,
                first.Project.LastModifiedUtc,
                ValidEvents(),
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            Assert.NotNull(rerun);
            var rerunOnsetIds = rerun.Project.PerformanceObservations
                .Where(item => item.AnalyzerId == OnsetObservationReport.AnalyzerId)
                .Select(item => item.Id)
                .ToHashSet();
            Assert.Equal(2, rerunOnsetIds.Count);
            Assert.Empty(rerunOnsetIds.Intersect(firstOnsetIds));
            Assert.Contains(rerun.Project.PerformanceObservations, item => item.Id == loudnessObservation.Id);
            Assert.Contains(rerun.Project.PerformanceObservations, item => item.Id == pitchObservation.Id);

            var cleared = await workspace.ReplaceOnsetObservationsAsync(
                saved.Project.Id,
                asset.Id,
                rerun.Project.LastModifiedUtc,
                [],
                DateTimeOffset.UtcNow,
                CancellationToken.None);

            Assert.NotNull(cleared);
            Assert.DoesNotContain(cleared.Project.PerformanceObservations, item => item.AnalyzerId == OnsetObservationReport.AnalyzerId);
            Assert.Contains(cleared.Project.PerformanceObservations, item => item.Id == loudnessObservation.Id);
            Assert.Contains(cleared.Project.PerformanceObservations, item => item.Id == pitchObservation.Id);
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
    public async Task Workspace_RejectsStaleOnsetReportBeforePersistingEvidence()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-onset-stale-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Stale onset", CancellationToken.None);

            await Assert.ThrowsAsync<StaleProjectSessionException>(() => workspace.ReplaceOnsetObservationsAsync(
                editor.Project.Id,
                ProjectAssetId.New(),
                editor.Project.LastModifiedUtc.AddSeconds(-1),
                ValidEvents(),
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

    private static IReadOnlyList<OnsetEventReport> ValidEvents() =>
    [
        new OnsetEventReport(160, 32, .72m, .91m),
        new OnsetEventReport(560, 32, .64m, .87m)
    ];
}
