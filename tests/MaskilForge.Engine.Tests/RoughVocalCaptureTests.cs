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
