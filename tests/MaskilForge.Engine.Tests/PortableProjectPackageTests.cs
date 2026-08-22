using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MaskilForge.Api;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class PortableProjectPackageTests
{
    [Fact]
    public void Package_RoundTripsSongGraphAndVerifiedOriginalVocalBytes()
    {
        var content = Encoding.UTF8.GetBytes("artist-owned original vocal bytes");
        var project = CreateProjectWithAsset(content, out var asset);
        var assets = new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = content };

        var package = PortableProjectPackage.Export(project, assets);
        var inspected = PortableProjectPackage.Inspect(package);

        Assert.Equal(project.Id, inspected.Project.Id);
        Assert.Equal(project.Title, inspected.Project.Title);
        Assert.Equal(asset, Assert.Single(inspected.Project.Assets));
        Assert.Equal(content, inspected.Assets[asset.Id]);
        Assert.Equal(SchemaVersion.Current.Value, inspected.Project.SchemaVersion.Value);
        Assert.True(package.Length < PortableProjectPackage.MaximumByteLength);
    }

    [Fact]
    public void Schema22Package_MigratesOriginalVocalTakesToDurableNames()
    {
        var content = Encoding.UTF8.GetBytes("legacy named vocal bytes");
        var project = CreateProjectWithAsset(content, out var asset);
        var package = PortableProjectPackage.Export(project, new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = content });
        var legacyProject = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        legacyProject["schemaVersion"] = 22;
        foreach (var legacyAsset in legacyProject["assets"]!.AsArray().OfType<JsonObject>())
            legacyAsset.Remove("name");

        var migratedPackage = MutateEntry(package, "project.json", Encoding.UTF8.GetBytes(legacyProject.ToJsonString()));
        var inspected = PortableProjectPackage.Inspect(migratedPackage);

        Assert.Equal(22, inspected.SourceSchemaVersion);
        Assert.Equal(24, inspected.Project.SchemaVersion.Value);
        Assert.Equal("Take 1", Assert.Single(inspected.Project.Assets).Name);
        Assert.Equal(content, inspected.Assets[asset.Id]);
    }

    [Fact]
    public void Package_RefusesMissingTamperedOrUnnamedMedia()
    {
        var content = Encoding.UTF8.GetBytes("immutable singer performance");
        var project = CreateProjectWithAsset(content, out var asset);
        var assets = new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = content };
        var package = PortableProjectPackage.Export(project, assets);

        var missing = Assert.Throws<InvalidProjectDataException>(() =>
            PortableProjectPackage.Export(project, new Dictionary<ProjectAssetId, byte[]>()));
        Assert.Contains(asset.Id.ToString(), missing.Message);

        var tamperedBytes = Encoding.UTF8.GetBytes("immutable singer performancX");
        var tampered = new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = tamperedBytes };
        var digestError = Assert.Throws<InvalidProjectDataException>(() => PortableProjectPackage.Export(project, tampered));
        Assert.Contains("SHA-256", digestError.Message);

        var extraId = ProjectAssetId.New();
        var extra = new Dictionary<ProjectAssetId, byte[]>(assets) { [extraId] = content };
        var extraError = Assert.Throws<InvalidProjectDataException>(() => PortableProjectPackage.Export(project, extra));
        Assert.Contains("does not name", extraError.Message);

        var mutated = MutateEntry(package, $"assets/{asset.Id}.bin", Encoding.UTF8.GetBytes(new string('x', content.Length)));
        var importError = Assert.Throws<InvalidProjectDataException>(() => PortableProjectPackage.Inspect(mutated));
        Assert.Contains("SHA-256", importError.Message);

        var unexpected = MutateEntry(package, "notes.txt", Encoding.UTF8.GetBytes("not media"));
        var unexpectedError = Assert.Throws<InvalidProjectDataException>(() => PortableProjectPackage.Inspect(unexpected));
        Assert.Contains("unexpected file", unexpectedError.Message);
    }

    [Fact]
    public void Package_ImportAsCopyKeepsVerifiedBytesAndCreatesANewProjectIdentity()
    {
        var content = Encoding.UTF8.GetBytes("copied original take");
        var project = CreateProjectWithAsset(content, out var asset);
        var package = PortableProjectPackage.Export(project, new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = content });

        var copy = PortableProjectPackage.Inspect(package, true);

        Assert.NotEqual(project.Id, copy.Project.Id);
        Assert.EndsWith(" (Imported Copy)", copy.Project.Title);
        Assert.Equal(asset.Id, copy.Project.Assets.Single().Id);
        Assert.Equal(content, copy.Assets[asset.Id]);
        Assert.Equal(SchemaVersion.Current.Value, copy.Project.SchemaVersion.Value);
        Assert.Equal(project.Artist, copy.Project.Artist);
    }

    [Fact]
    public void JsonPortability_StillRefusesReferencedMediaWithoutBytes()
    {
        var project = CreateProjectWithAsset(Encoding.UTF8.GetBytes("left behind"), out _);
        var exportError = Assert.Throws<InvalidOperationException>(() => PortableProjectExporter.Export(project));
        Assert.Contains(".maskil package", exportError.Message);

        var json = Encoding.UTF8.GetString(PortableProjectExporter.SerializeDocument(project));
        var importError = Assert.Throws<InvalidProjectDataException>(() => PortableProjectImporter.Import(json));
        Assert.Contains(".maskil package", importError.Message);
    }

    [Fact]
    public async Task Repository_ImportsPackageBytesAndDuplicatesThemWithANewProjectIdentity()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-package-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var content = Encoding.UTF8.GetBytes("repository-owned original vocal");
            var project = SongProject.Create("Packaged voice");
            var asset = CreateAsset(content);
            project.RegisterAsset(asset);
            await repository.SaveWithAssetAsync(project, asset, new MemoryStream(content));

            var loaded = await repository.LoadAsync(project.Id);
        var opened = await repository.OpenAssetAsync(project.Id, asset.Id);
        using var buffer = new MemoryStream();
        await using (opened)
            await opened!.CopyToAsync(buffer);
            var package = PortableProjectPackage.Export(loaded!, new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = buffer.ToArray() });

            var imported = PortableProjectPackage.Inspect(package, true);
            var editor = await workspace.ImportWithAssetsAsync(imported.Project, imported.Assets, CancellationToken.None);
            await using (var importedStream = await repository.OpenAssetAsync(editor.Project.Id, asset.Id))
            {
                using var importedBytes = new MemoryStream();
                await importedStream!.CopyToAsync(importedBytes);
                Assert.Equal(content, importedBytes.ToArray());
            }
            Assert.NotEqual(project.Id, editor.Project.Id);

            var duplicate = await workspace.DuplicateAsync(editor.Project.Id, CancellationToken.None);
            Assert.NotNull(duplicate);
            Assert.NotEqual(editor.Project.Id, duplicate.Project.Id);
            Assert.Equal(asset.Id, duplicate.Project.Assets.Single().Id);
            await using (var duplicatedStream = await repository.OpenAssetAsync(duplicate.Project.Id, asset.Id))
            {
                using var duplicatedBytes = new MemoryStream();
                await duplicatedStream!.CopyToAsync(duplicatedBytes);
                Assert.Equal(content, duplicatedBytes.ToArray());
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ImportAsync(loaded!));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static SongProject CreateProjectWithAsset(byte[] content, out ProjectAsset asset)
    {
        var project = SongProject.Create("Recording attached");
        project.SetArtist("Portable Artist");
        asset = CreateAsset(content);
        project.RegisterAsset(asset);
        return project;
    }

    private static ProjectAsset CreateAsset(byte[] content) => new(
        ProjectAssetId.New(),
        ProjectAssetKind.OriginalVocalTake,
        "audio/webm;codecs=opus",
        content.LongLength,
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
        new DateTimeOffset(2026, 8, 14, 20, 0, 0, TimeSpan.Zero),
        "Take 1");

    private static byte[] MutateEntry(byte[] package, string name, byte[] content)
    {
        using var input = new MemoryStream(package);
        using var output = new MemoryStream();
        using (var source = new ZipArchive(input, ZipArchiveMode.Read))
        using (var destination = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in source.Entries)
            {
                var copy = destination.CreateEntry(entry.FullName);
                using var destinationStream = copy.Open();
                if (string.Equals(entry.FullName, name, StringComparison.Ordinal))
                {
                    destinationStream.Write(content);
                    continue;
                }
                using var sourceStream = entry.Open();
                sourceStream.CopyTo(destinationStream);
            }

            if (source.Entries.All(entry => !string.Equals(entry.FullName, name, StringComparison.Ordinal)))
            {
                var extra = destination.CreateEntry(name);
                using var extraStream = extra.Open();
                extraStream.Write(content);
            }
        }

        return output.ToArray();
    }
}
