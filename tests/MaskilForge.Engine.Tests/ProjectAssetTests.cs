using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MaskilForge.Domain;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class ProjectAssetTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void OriginalVocalAsset_RequiresPortableIntegrityMetadata()
    {
        var created = new DateTimeOffset(2026, 8, 14, 20, 0, 0, TimeSpan.Zero);
        var asset = new ProjectAsset(
            ProjectAssetId.New(),
            ProjectAssetKind.OriginalVocalTake,
            " Audio/WebM;Codecs=Opus ",
            12_345,
            new string('A', 64),
            created,
            "  First chorus idea  ");

        Assert.Equal("audio/webm;codecs=opus", asset.MediaType);
        Assert.Equal(new string('a', 64), asset.Sha256);
        Assert.Equal(12_345, asset.ByteLength);
        Assert.Equal(created, asset.CreatedUtc);
        Assert.Equal("First chorus idea", asset.Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectAsset(
            ProjectAssetId.New(), ProjectAssetKind.OriginalVocalTake, "audio/webm", 0, new string('a', 64), created, "Take 1"));
        Assert.Throws<ArgumentException>(() => new ProjectAsset(
            ProjectAssetId.New(), ProjectAssetKind.OriginalVocalTake, "audio/webm", 1, "not-a-hash", created, "Take 1"));
        Assert.Throws<ArgumentException>(() => asset.Rename("  "));
        Assert.Throws<ArgumentOutOfRangeException>(() => asset.Rename(new string('x', 81)));
    }

    [Fact]
    public void SongProject_RegistersAndRemovesAssetIdentityExplicitly()
    {
        var project = SongProject.Create("Human voice");
        var asset = CreateAsset();

        project.RegisterAsset(asset);
        Assert.Same(asset, Assert.Single(project.Assets));
        Assert.Throws<InvalidOperationException>(() => project.RegisterAsset(asset));

        Assert.Same(asset, project.RemoveAsset(asset.Id));
        Assert.Empty(project.Assets);
        Assert.Throws<KeyNotFoundException>(() => project.RemoveAsset(asset.Id));
    }

    [Fact]
    public async Task ProjectRepository_RoundTripsPathFreeAssetManifest()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-assets-{Guid.NewGuid():N}");
        try
        {
            var project = SongProject.Create("Manifest song");
            var content = Encoding.UTF8.GetBytes("artist-owned original vocal bytes");
            var asset = CreateAsset(content);
            project.RegisterAsset(asset);
            var repository = new JsonFileProjectRepository(directory);

            await repository.SaveWithAssetAsync(project, asset, new MemoryStream(content));
            var loaded = await repository.LoadAsync(project.Id);

            var loadedAsset = Assert.Single(loaded!.Assets);
            Assert.Equal(asset, loadedAsset);
            var json = await File.ReadAllTextAsync(Path.Combine(directory, $"{project.Id}.json"));
            Assert.DoesNotContain(directory, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("path", json, StringComparison.OrdinalIgnoreCase);
            await using var opened = await repository.OpenAssetAsync(project.Id, asset.Id);
            using var copied = new MemoryStream();
            await opened!.CopyToAsync(copied);
            Assert.Equal(content, copied.ToArray());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task AssetBytes_FollowBackupRecoveryTrashRestoreAndPermanentDeletion()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-asset-life-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Lifecycle voice");
            await repository.SaveAsync(project);
            var content = Encoding.UTF8.GetBytes("immutable singer performance");
            var asset = CreateAsset(content);
            project.RegisterAsset(asset);
            await repository.SaveWithAssetAsync(project, asset, new MemoryStream(content));

            project.SetDescription("Create a paired known-good backup.");
            await repository.SaveAsync(project);
            var activeAsset = Path.Combine(directory, $"{project.Id}.assets", $"{asset.Id}.bin");
            var backupAsset = Path.Combine(directory, "backups", $"{project.Id}.assets", $"{asset.Id}.bin");
            Assert.True(File.Exists(activeAsset));
            Assert.True(File.Exists(backupAsset));

            await repository.SaveRecoverySnapshotAsync(new ProjectRecoverySnapshot(
                project,
                DateTimeOffset.UtcNow,
                project.LastModifiedUtc,
                "asset-session"));
            var recoveryAsset = Path.Combine(directory, "sessions", $"{project.Id}.assets", $"{asset.Id}.bin");
            Assert.True(File.Exists(recoveryAsset));

            Assert.True(await repository.MoveToTrashAsync(project.Id));
            Assert.False(File.Exists(activeAsset));
            var trashedAsset = Assert.Single(Directory.EnumerateFiles(Path.Combine(directory, "trash"), $"{asset.Id}.bin", SearchOption.AllDirectories));
            Assert.True(File.Exists(trashedAsset));
            Assert.False(File.Exists(recoveryAsset));

            Assert.True(await repository.RestoreFromTrashAsync(project.Id));
            Assert.True(File.Exists(activeAsset));
            Assert.NotNull(await repository.LoadAsync(project.Id));

            Assert.True(await repository.MoveToTrashAsync(project.Id));
            Assert.True(await repository.PermanentlyDeleteAsync(project.Id));
            Assert.False(Directory.Exists(Path.Combine(directory, $"{project.Id}.assets")));
            Assert.False(Directory.Exists(Path.Combine(directory, "backups", $"{project.Id}.assets")));
            Assert.Empty(Directory.EnumerateFiles(directory, $"{asset.Id}.bin", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task TamperedAsset_IsRejectedAndPreservedWithItsRecoveryCopy()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-asset-tamper-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Integrity voice");
            var content = Encoding.UTF8.GetBytes("original take");
            var asset = CreateAsset(content);
            project.RegisterAsset(asset);
            await repository.SaveWithAssetAsync(project, asset, new MemoryStream(content));
            var activeAsset = Path.Combine(directory, $"{project.Id}.assets", $"{asset.Id}.bin");
            await File.WriteAllBytesAsync(activeAsset, Encoding.UTF8.GetBytes("tampered take"));

            var exception = await Assert.ThrowsAsync<MaskilForge.Engine.InvalidProjectDataException>(() => repository.LoadAsync(project.Id));
            Assert.Contains("SHA-256", exception.InnerException?.Message ?? exception.Message);
            var recoveryJson = Assert.Single(Directory.EnumerateFiles(Path.Combine(directory, "recovery"), $"{project.Id}-*.json"));
            Assert.True(File.Exists(Path.Combine(
                Path.ChangeExtension(recoveryJson, null) + ".assets",
                $"{asset.Id}.bin")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Schema21_MigratesToAnExplicitEmptyAssetManifest()
    {
        var project = SongProject.Create("Legacy media boundary");
        var document = JsonNode.Parse(PortableProjectExporter.Export(project))!.AsObject();
        document["schemaVersion"] = 21;
        document.Remove("assets");

        var inspected = PortableProjectImporter.Inspect(document.ToJsonString());

        Assert.Equal(21, inspected.SourceSchemaVersion);
        Assert.Equal(27, inspected.Project.SchemaVersion.Value);
        Assert.Empty(inspected.Project.Assets);
    }

    [Fact]
    public void LegacyJsonPortability_RefusesToLeaveReferencedRecordingBytesBehind()
    {
        var project = SongProject.Create("Recording attached");
        project.RegisterAsset(CreateAsset());

        var exportError = Assert.Throws<InvalidOperationException>(() => PortableProjectExporter.Export(project));
        Assert.Contains("cannot carry original recordings", exportError.Message);

        var json = JsonSerializer.Serialize(project, JsonOptions);
        var importError = Assert.Throws<MaskilForge.Engine.InvalidProjectDataException>(() => PortableProjectImporter.Import(json));
        Assert.Contains("without carrying its bytes", importError.Message);
    }

    private static ProjectAsset CreateAsset() => new(
        ProjectAssetId.New(),
        ProjectAssetKind.OriginalVocalTake,
        "audio/webm;codecs=opus",
        4_096,
        new string('b', 64),
        new DateTimeOffset(2026, 8, 14, 20, 0, 0, TimeSpan.Zero),
        "Take 1");

    private static ProjectAsset CreateAsset(byte[] content) => new(
        ProjectAssetId.New(),
        ProjectAssetKind.OriginalVocalTake,
        "audio/webm;codecs=opus",
        content.LongLength,
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
        new DateTimeOffset(2026, 8, 14, 20, 0, 0, TimeSpan.Zero),
        "Take 1");
}
