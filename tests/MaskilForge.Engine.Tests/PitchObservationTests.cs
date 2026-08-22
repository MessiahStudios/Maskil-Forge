using System.Text;
using MaskilForge.Api;
using MaskilForge.Domain;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class PitchObservationTests
{
    [Fact]
    public void ReportBoundary_StampsConfidenceAndRejectsInvalidPitchClaims()
    {
        var sourceAssetId = ProjectAssetId.New();
        var createdUtc = new DateTimeOffset(2026, 8, 22, 6, 0, 0, TimeSpan.Zero);
        var observations = PitchObservationReport.CreateObservations(sourceAssetId, ValidFrames(), createdUtc);

        Assert.Equal(2, observations.Count);
        Assert.All(observations, observation =>
        {
            Assert.Equal(sourceAssetId, observation.SourceAssetId);
            Assert.Equal(PitchObservationReport.ObservationKind, observation.Kind);
            Assert.Equal(PitchObservationReport.AnalyzerId, observation.AnalyzerId);
            Assert.Equal(PitchObservationReport.AnalyzerVersion, observation.AnalyzerVersion);
            Assert.Equal(PerformanceObservationProvenance.DeterministicAnalyzer, observation.Provenance);
            Assert.InRange(observation.Confidence!.Value, PitchObservationReport.MinimumConfidence, 1);
            var measurement = Assert.Single(observation.Measurements);
            Assert.Equal("frequencyHertz", measurement.Name);
            Assert.Equal("hertz", measurement.Unit);
            Assert.Equal(createdUtc, observation.CreatedUtc);
        });
        Assert.Empty(PitchObservationReport.CreateObservations(sourceAssetId, [], createdUtc));

        Assert.Throws<ArgumentException>(() => PitchObservationReport.CreateObservations(sourceAssetId,
            [new PitchFrameReport(1, 80, 440, .9m)], createdUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => PitchObservationReport.CreateObservations(sourceAssetId,
            [new PitchFrameReport(0, 100, 440, .9m)], createdUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => PitchObservationReport.CreateObservations(sourceAssetId,
            [new PitchFrameReport(0, 80, 40, .9m)], createdUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => PitchObservationReport.CreateObservations(sourceAssetId,
            [new PitchFrameReport(0, 80, 440, .5m)], createdUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => PitchObservationReport.CreateObservations(sourceAssetId,
            [new PitchFrameReport(long.MaxValue - (long.MaxValue % 200), 80, 440, .9m)], createdUtc));
    }

    [Fact]
    public async Task Workspace_RerunReplacesOrClearsOnlyPitchFramesWithoutChangingSourceBytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-pitch-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Pitch evidence", CancellationToken.None);
            var content = Encoding.UTF8.GetBytes("immutable voiced source bytes");
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
            saved.Project.RegisterPerformanceObservation(loudnessObservation);
            await repository.SaveAsync(saved.Project);

            var first = await workspace.ReplacePitchObservationsAsync(
                saved.Project.Id,
                asset.Id,
                saved.Project.LastModifiedUtc,
                ValidFrames(),
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            Assert.NotNull(first);
            var firstPitchIds = first.Project.PerformanceObservations
                .Where(item => item.AnalyzerId == PitchObservationReport.AnalyzerId)
                .Select(item => item.Id)
                .ToHashSet();
            Assert.Equal(2, firstPitchIds.Count);
            Assert.Contains(first.Project.PerformanceObservations, item => item.Id == loudnessObservation.Id);

            var rerun = await workspace.ReplacePitchObservationsAsync(
                saved.Project.Id,
                asset.Id,
                first.Project.LastModifiedUtc,
                ValidFrames(),
                DateTimeOffset.UtcNow,
                CancellationToken.None);

            Assert.NotNull(rerun);
            var rerunPitchIds = rerun.Project.PerformanceObservations
                .Where(item => item.AnalyzerId == PitchObservationReport.AnalyzerId)
                .Select(item => item.Id)
                .ToHashSet();
            Assert.Equal(2, rerunPitchIds.Count);
            Assert.Empty(rerunPitchIds.Intersect(firstPitchIds));
            Assert.Contains(rerun.Project.PerformanceObservations, item => item.Id == loudnessObservation.Id);

            var cleared = await workspace.ReplacePitchObservationsAsync(
                saved.Project.Id,
                asset.Id,
                rerun.Project.LastModifiedUtc,
                [],
                DateTimeOffset.UtcNow,
                CancellationToken.None);

            Assert.NotNull(cleared);
            Assert.DoesNotContain(cleared.Project.PerformanceObservations, item => item.AnalyzerId == PitchObservationReport.AnalyzerId);
            Assert.Contains(cleared.Project.PerformanceObservations, item => item.Id == loudnessObservation.Id);
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
    public async Task Workspace_RejectsStalePitchReportBeforePersistingEvidence()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-pitch-stale-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Stale pitch", CancellationToken.None);

            await Assert.ThrowsAsync<StaleProjectSessionException>(() => workspace.ReplacePitchObservationsAsync(
                editor.Project.Id,
                ProjectAssetId.New(),
                editor.Project.LastModifiedUtc.AddSeconds(-1),
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

    private static IReadOnlyList<PitchFrameReport> ValidFrames() =>
    [
        new PitchFrameReport(0, 80, 440.25m, .96m),
        new PitchFrameReport(400, 80, 220.1m, .88m)
    ];
}
