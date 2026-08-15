using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Infrastructure;

/// <summary>
/// Exports and inspects an asset-owning Maskil project package.
/// The archive carries the Song Graph plus every referenced original-media byte.
/// </summary>
public static class PortableProjectPackage
{
    public const string FileExtension = ".maskil";
    public const string ContentType = "application/vnd.maskil-forge.project+zip";
    public const int PackageVersion = 1;
    public const long MaximumByteLength = 25 * 1024 * 1024;
    private const string PackageManifestEntry = "maskil-package.json";
    private const string ProjectEntry = "project.json";
    private const string AssetsDirectory = "assets/";
    private static readonly DateTimeOffset DeterministicTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static byte[] Export(SongProject project, IReadOnlyDictionary<ProjectAssetId, byte[]> assets)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(assets);
        ValidateCompleteAssetSet(project, assets);

        using var archiveStream = new MemoryStream();
        using (var zip = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
        {
            WriteEntry(zip, PackageManifestEntry, JsonSerializer.SerializeToUtf8Bytes(new PackageManifest("maskil-forge-project", PackageVersion), JsonOptions));
            WriteEntry(zip, ProjectEntry, PortableProjectExporter.SerializeDocument(project));
            foreach (var asset in project.Assets.OrderBy(item => item.Id.Value))
                WriteEntry(zip, AssetEntryName(asset.Id), assets[asset.Id]);
        }

        return archiveStream.ToArray();
    }

    public static PortableProjectPackageDocument Inspect(byte[] packageBytes, bool importAsCopy = false)
    {
        ArgumentNullException.ThrowIfNull(packageBytes);
        if (packageBytes.Length == 0)
            throw new InvalidProjectDataException("The project package is empty.");
        if (packageBytes.Length > MaximumByteLength)
            throw new InvalidProjectDataException("Asset-owning project packages cannot exceed 25 MB.");

        try
        {
            using var archiveStream = new MemoryStream(packageBytes, false);
            using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read);
            var entries = zip.Entries.ToDictionary(entry => NormalizeEntryName(entry.FullName), StringComparer.Ordinal);
            var packageManifest = ReadPackageManifest(ReadRequiredEntry(entries, PackageManifestEntry));
            if (packageManifest.PackageVersion > PackageVersion)
                throw new InvalidProjectDataException(
                    $"This project package uses format version {packageManifest.PackageVersion}, but this version of Maskil Forge supports up to version {PackageVersion}.");
            if (packageManifest.PackageVersion < 1 || !string.Equals(packageManifest.Format, "maskil-forge-project", StringComparison.Ordinal))
                throw new InvalidProjectDataException("The project package does not declare a supported Maskil Forge package format.");

            var document = PortableProjectImporter.InspectDocument(ReadRequiredEntry(entries, ProjectEntry), importAsCopy);
            var assets = ReadVerifiedAssets(document.Project, entries);
            return new PortableProjectPackageDocument(document.Project, document.SourceSchemaVersion, assets);
        }
        catch (ProjectPersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            throw new CorruptProjectException("The project package is not a valid Maskil Forge archive.", null, exception);
        }
    }

    public static SongProject Import(byte[] packageBytes) => Inspect(packageBytes).Project;

    public static SongProject ImportAsCopy(byte[] packageBytes) => Inspect(packageBytes, true).Project;

    internal static void ValidateCompleteAssetSet(SongProject project, IReadOnlyDictionary<ProjectAssetId, byte[]> assets)
    {
        foreach (var asset in project.Assets)
        {
            if (!assets.TryGetValue(asset.Id, out var content))
                throw new InvalidProjectDataException($"Project asset '{asset.Id}' is missing from the portable package.");
            ValidateAssetBytes(asset, content);
        }

        foreach (var assetId in assets.Keys)
        {
            if (project.Assets.All(asset => asset.Id != assetId))
                throw new InvalidProjectDataException($"The portable package includes media that the project manifest does not name: '{assetId}'.");
        }
    }

    internal static void ValidateAssetBytes(ProjectAsset asset, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(content);
        if (content.LongLength != asset.ByteLength)
            throw new InvalidProjectDataException($"Project asset '{asset.Id}' byte length does not match its manifest.");
        var digest = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!string.Equals(digest, asset.Sha256, StringComparison.Ordinal))
            throw new InvalidProjectDataException($"Project asset '{asset.Id}' SHA-256 does not match its manifest.");
    }

    private static IReadOnlyDictionary<ProjectAssetId, byte[]> ReadVerifiedAssets(
        SongProject project,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        var assets = new Dictionary<ProjectAssetId, byte[]>();
        foreach (var entry in entries)
        {
            var name = entry.Key;
            if (name is PackageManifestEntry or ProjectEntry or AssetsDirectory) continue;
            if (entry.Value.Length == 0 && name.EndsWith('/'))
                throw new InvalidProjectDataException($"The project package contains an unexpected folder '{name}'.");
            if (!name.StartsWith(AssetsDirectory, StringComparison.Ordinal) || name.IndexOf('/', AssetsDirectory.Length) >= 0)
                throw new InvalidProjectDataException($"The project package contains an unexpected file '{name}'.");
            var fileName = name[AssetsDirectory.Length..];
            if (!fileName.EndsWith(".bin", StringComparison.Ordinal) || !Guid.TryParse(fileName[..^4], out var assetIdValue))
                throw new InvalidProjectDataException($"The project package contains an unexpected media file '{name}'.");
            using var stream = entry.Value.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            assets[new ProjectAssetId(assetIdValue)] = buffer.ToArray();
        }

        ValidateCompleteAssetSet(project, assets);
        return assets;
    }

    private static byte[] ReadRequiredEntry(IReadOnlyDictionary<string, ZipArchiveEntry> entries, string name)
    {
        if (!entries.TryGetValue(name, out var entry))
            throw new InvalidProjectDataException($"The project package is missing '{name}'.");
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static PackageManifest ReadPackageManifest(byte[] json)
    {
        var manifest = JsonSerializer.Deserialize<PackageManifest>(json, JsonOptions)
            ?? throw new InvalidProjectDataException("The project package does not declare a Maskil Forge package format.");
        return manifest;
    }

    private static void WriteEntry(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        entry.LastWriteTime = DeterministicTimestamp;
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static string AssetEntryName(ProjectAssetId assetId) => $"{AssetsDirectory}{assetId}.bin";

    private static string NormalizeEntryName(string name) => name.Replace('\\', '/').TrimStart('/');

    private sealed record PackageManifest(string Format, int PackageVersion);
}

public sealed record PortableProjectPackageDocument(
    SongProject Project,
    int SourceSchemaVersion,
    IReadOnlyDictionary<ProjectAssetId, byte[]> Assets);
