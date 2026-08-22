using System.Text;
using MaskilForge.Api;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class RoughVocalCaptureTests
{
    [Fact]
    public async Task Workspace_AttachesVerifiedOriginalVocalBytesToThePersistedRevision()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-rough-vocal-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Phone vocal", CancellationToken.None);
            var persistedRevision = editor.Project.LastModifiedUtc;
            var content = Encoding.UTF8.GetBytes("artist-reviewed rough vocal bytes");

            var updated = await workspace.AddOriginalVocalTakeAsync(
                editor.Project.Id,
                persistedRevision,
                "audio/webm;codecs=opus",
                content,
                new DateTimeOffset(2026, 8, 21, 1, 0, 0, TimeSpan.Zero),
                CancellationToken.None);

            Assert.NotNull(updated);
            var asset = Assert.Single(updated.Project.Assets);
            Assert.Equal(ProjectAssetKind.OriginalVocalTake, asset.Kind);
            Assert.Equal(content.LongLength, asset.ByteLength);
            await using var stored = await repository.OpenAssetAsync(updated.Project.Id, asset.Id);
            using var copy = new MemoryStream();
            await stored!.CopyToAsync(copy);
            Assert.Equal(content, copy.ToArray());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Workspace_RejectsAStaleRecordingWithoutRegisteringOrWritingIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-stale-vocal-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Stale phone vocal", CancellationToken.None);

            await Assert.ThrowsAsync<StaleProjectSessionException>(() => workspace.AddOriginalVocalTakeAsync(
                editor.Project.Id,
                editor.Project.LastModifiedUtc.AddSeconds(-1),
                "audio/webm;codecs=opus",
                Encoding.UTF8.GetBytes("must not persist"),
                DateTimeOffset.UtcNow,
                CancellationToken.None));

            var persisted = await repository.LoadAsync(editor.Project.Id);
            Assert.Empty(persisted!.Assets);
            Assert.False(Directory.Exists(Path.Combine(directory, $"{editor.Project.Id}.assets")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Workspace_RemovesOneTakeFromTheCurrentVersionAndPreservesThePreviousBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-remove-vocal-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Take removal", CancellationToken.None);
            var first = await workspace.AddOriginalVocalTakeAsync(
                editor.Project.Id,
                editor.Project.LastModifiedUtc,
                "audio/webm;codecs=opus",
                Encoding.UTF8.GetBytes("first take"),
                new DateTimeOffset(2026, 8, 21, 2, 0, 0, TimeSpan.Zero),
                CancellationToken.None);
            var second = await workspace.AddOriginalVocalTakeAsync(
                editor.Project.Id,
                first!.Project.LastModifiedUtc,
                "audio/webm;codecs=opus",
                Encoding.UTF8.GetBytes("second take"),
                new DateTimeOffset(2026, 8, 21, 2, 1, 0, TimeSpan.Zero),
                CancellationToken.None);
            var removedAsset = first.Project.Assets[0];
            var retainedAsset = second!.Project.Assets[1];

            var updated = await workspace.RemoveOriginalVocalTakeAsync(
                editor.Project.Id,
                removedAsset.Id,
                second.Project.LastModifiedUtc,
                CancellationToken.None);

            Assert.NotNull(updated);
            Assert.Equal(retainedAsset.Id, Assert.Single(updated.Project.Assets).Id);
            Assert.False(File.Exists(Path.Combine(directory, $"{editor.Project.Id}.assets", $"{removedAsset.Id}.bin")));
            Assert.True(File.Exists(Path.Combine(directory, $"{editor.Project.Id}.assets", $"{retainedAsset.Id}.bin")));
            Assert.True(File.Exists(Path.Combine(directory, "backups", $"{editor.Project.Id}.assets", $"{removedAsset.Id}.bin")));
            Assert.Null(await repository.OpenAssetAsync(editor.Project.Id, removedAsset.Id));
            await using var retained = await repository.OpenAssetAsync(editor.Project.Id, retainedAsset.Id);
            using var copy = new MemoryStream();
            await retained!.CopyToAsync(copy);
            Assert.Equal("second take", Encoding.UTF8.GetString(copy.ToArray()));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Workspace_RejectsStaleTakeRemovalWithoutChangingManifestOrBytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-stale-remove-vocal-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Stale take removal", CancellationToken.None);
            var saved = await workspace.AddOriginalVocalTakeAsync(
                editor.Project.Id,
                editor.Project.LastModifiedUtc,
                "audio/webm;codecs=opus",
                Encoding.UTF8.GetBytes("keep this take"),
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            var asset = Assert.Single(saved!.Project.Assets);

            await Assert.ThrowsAsync<StaleProjectSessionException>(() => workspace.RemoveOriginalVocalTakeAsync(
                editor.Project.Id,
                asset.Id,
                saved.Project.LastModifiedUtc.AddSeconds(-1),
                CancellationToken.None));

            var persisted = await repository.LoadAsync(editor.Project.Id);
            Assert.Equal(asset.Id, Assert.Single(persisted!.Assets).Id);
            Assert.True(File.Exists(Path.Combine(directory, $"{editor.Project.Id}.assets", $"{asset.Id}.bin")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task UploadBoundary_ValidatesFormatSizeAndNonEmptyAudio()
    {
        Assert.Equal("audio/webm;codecs=opus", OriginalVocalTakeUpload.NormalizeMediaType(" Audio/WebM;Codecs=Opus "));
        Assert.Throws<ArgumentException>(() => OriginalVocalTakeUpload.NormalizeMediaType("audio/wav"));

        var content = Encoding.UTF8.GetBytes("take");
        Assert.Equal(content, await OriginalVocalTakeUpload.ReadAsync(
            new MemoryStream(content),
            content.Length,
            CancellationToken.None,
            maximumByteLength: 4));
        await Assert.ThrowsAsync<ArgumentException>(() => OriginalVocalTakeUpload.ReadAsync(
            new MemoryStream(content),
            content.Length,
            CancellationToken.None,
            maximumByteLength: 3));
        await Assert.ThrowsAsync<ArgumentException>(() => OriginalVocalTakeUpload.ReadAsync(
            new MemoryStream(),
            0,
            CancellationToken.None,
            maximumByteLength: 4));
    }
}
