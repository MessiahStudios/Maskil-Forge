using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MaskilForge.Api;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class PerformanceObservationTests
{
    [Fact]
    public void Observation_RequiresTypedMeasurementsAnalyzerIdentityAndBoundedConfidence()
    {
        var sourceAssetId = ProjectAssetId.New();
        var createdUtc = new DateTimeOffset(2026, 8, 21, 20, 0, 0, TimeSpan.Zero);
        var observation = CreateObservation(sourceAssetId, createdUtc);

        Assert.Equal("pitch.frame", observation.Kind);
        Assert.Equal(125, observation.StartMilliseconds);
        Assert.Equal(20, observation.DurationMilliseconds);
        Assert.Equal(440.25m, Assert.Single(observation.Measurements).Value);
        Assert.Equal("hertz", observation.Measurements[0].Unit);
        Assert.Equal(0.875m, observation.Confidence);
        Assert.Equal("maskil.pitch-frame", observation.AnalyzerId);
        Assert.Equal("1.0.0", observation.AnalyzerVersion);
        Assert.Equal(PerformanceObservationProvenance.DeterministicAnalyzer, observation.Provenance);

        Assert.Throws<ArgumentOutOfRangeException>(() => new PerformanceObservation(
            PerformanceObservationId.New(), sourceAssetId, "pitch.frame", 0, 10,
            [new PerformanceMeasurement("frequency", 440, "hertz")], 1.01m,
            "analyzer", "1", PerformanceObservationProvenance.DeterministicAnalyzer, createdUtc));
        Assert.Throws<ArgumentException>(() => new PerformanceObservation(
            PerformanceObservationId.New(), sourceAssetId, "pitch.frame", 0, 10,
            [new PerformanceMeasurement("frequency", 440, "hertz"), new PerformanceMeasurement("Frequency", 69, "midi-note")], .9m,
            "analyzer", "1", PerformanceObservationProvenance.DeterministicAnalyzer, createdUtc));
    }

    [Fact]
    public void SongProject_RequiresOriginalVocalOwnershipAndCascadesAssetRemoval()
    {
        var project = SongProject.Create("Observed voice");
        var content = Encoding.UTF8.GetBytes("source performance");
        var asset = CreateAsset(content);
        var observation = CreateObservation(asset.Id, DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => project.RegisterPerformanceObservation(observation));
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        Assert.Same(observation, Assert.Single(project.PerformanceObservations));
        Assert.Throws<InvalidOperationException>(() => project.RegisterPerformanceObservation(observation));

        project.RemoveAsset(asset.Id);

        Assert.Empty(project.Assets);
        Assert.Empty(project.PerformanceObservations);
    }

    [Fact]
    public async Task RepositoryAndPackage_RoundTripObservationsWithoutChangingSourceBytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-observation-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Measured voice");
            var content = Encoding.UTF8.GetBytes("immutable observed performance");
            var asset = CreateAsset(content);
            project.RegisterAsset(asset);
            await repository.SaveWithAssetAsync(project, asset, new MemoryStream(content));
            var observation = CreateObservation(asset.Id, DateTimeOffset.UtcNow);
            project.RegisterPerformanceObservation(observation);
            await repository.SaveAsync(project);

            var loaded = await repository.LoadAsync(project.Id);
            AssertObservationEqual(observation, Assert.Single(loaded!.PerformanceObservations));
            await using var stored = await repository.OpenAssetAsync(project.Id, asset.Id);
            using var copied = new MemoryStream();
            await stored!.CopyToAsync(copied);
            Assert.Equal(content, copied.ToArray());

            var package = PortableProjectPackage.Export(loaded, new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = copied.ToArray() });
            var inspected = PortableProjectPackage.Inspect(package);
            AssertObservationEqual(observation, Assert.Single(inspected.Project.PerformanceObservations));
            Assert.Equal(content, inspected.Assets[asset.Id]);

            var duplicate = await new ProjectWorkspace(repository).DuplicateAsync(project.Id, CancellationToken.None);
            Assert.NotNull(duplicate);
            Assert.NotEqual(project.Id, duplicate.Project.Id);
            AssertObservationEqual(observation, Assert.Single(duplicate.Project.PerformanceObservations));
            await using var duplicatedSource = await repository.OpenAssetAsync(duplicate.Project.Id, asset.Id);
            using var duplicatedBytes = new MemoryStream();
            await duplicatedSource!.CopyToAsync(duplicatedBytes);
            Assert.Equal(content, duplicatedBytes.ToArray());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Workspace_RemovingSourceTakeAlsoRemovesItsObservations()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-observation-remove-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Observation cleanup", CancellationToken.None);
            var saved = await workspace.AddOriginalVocalTakeAsync(
                editor.Project.Id,
                editor.Project.LastModifiedUtc,
                "audio/webm",
                Encoding.UTF8.GetBytes("remove this observed take"),
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            var asset = Assert.Single(saved!.Project.Assets);
            saved.Project.RegisterPerformanceObservation(CreateObservation(asset.Id, DateTimeOffset.UtcNow));
            await repository.SaveAsync(saved.Project);

            var removed = await workspace.RemoveOriginalVocalTakeAsync(
                saved.Project.Id,
                asset.Id,
                saved.Project.LastModifiedUtc,
                CancellationToken.None);

            Assert.NotNull(removed);
            Assert.Empty(removed.Project.Assets);
            Assert.Empty(removed.Project.PerformanceObservations);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Schema23_MigratesToAnExplicitEmptyObservationCollection()
    {
        var project = SongProject.Create("Pre-observation song");
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        document["schemaVersion"] = 23;
        document.Remove("performanceObservations");

        var inspected = PortableProjectImporter.Inspect(document.ToJsonString());

        Assert.Equal(23, inspected.SourceSchemaVersion);
        Assert.Equal(25, inspected.Project.SchemaVersion.Value);
        Assert.Empty(inspected.Project.PerformanceObservations);
    }

    private static ProjectAsset CreateAsset(byte[] content) => new(
        ProjectAssetId.New(),
        ProjectAssetKind.OriginalVocalTake,
        "audio/webm",
        content.LongLength,
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
        DateTimeOffset.UtcNow,
        "Observed take");

    private static PerformanceObservation CreateObservation(ProjectAssetId sourceAssetId, DateTimeOffset createdUtc) => new(
        PerformanceObservationId.New(),
        sourceAssetId,
        " pitch.frame ",
        125,
        20,
        [new PerformanceMeasurement(" frequency ", 440.25m, " hertz ")],
        .875m,
        " maskil.pitch-frame ",
        " 1.0.0 ",
        PerformanceObservationProvenance.DeterministicAnalyzer,
        createdUtc);

    private static void AssertObservationEqual(PerformanceObservation expected, PerformanceObservation actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.SourceAssetId, actual.SourceAssetId);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.StartMilliseconds, actual.StartMilliseconds);
        Assert.Equal(expected.DurationMilliseconds, actual.DurationMilliseconds);
        Assert.Equal(expected.Measurements.Count, actual.Measurements.Count);
        for (var index = 0; index < expected.Measurements.Count; index++)
            Assert.Equal(expected.Measurements[index], actual.Measurements[index]);
        Assert.Equal(expected.Confidence, actual.Confidence);
        Assert.Equal(expected.AnalyzerId, actual.AnalyzerId);
        Assert.Equal(expected.AnalyzerVersion, actual.AnalyzerVersion);
        Assert.Equal(expected.Provenance, actual.Provenance);
        Assert.Equal(expected.CreatedUtc, actual.CreatedUtc);
    }
}
