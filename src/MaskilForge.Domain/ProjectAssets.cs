using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum ProjectAssetKind
{
    OriginalVocalTake
}

public sealed record ProjectAsset
{
    [JsonConstructor]
    public ProjectAsset(
        ProjectAssetId id,
        ProjectAssetKind kind,
        string mediaType,
        long byteLength,
        string sha256,
        DateTimeOffset createdUtc,
        string name)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A project asset ID is required.", nameof(id));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind), "Project asset kind is invalid.");
        if (string.IsNullOrWhiteSpace(mediaType)) throw new ArgumentException("A project asset media type is required.", nameof(mediaType));
        if (mediaType.Trim().Length > 200) throw new ArgumentOutOfRangeException(nameof(mediaType), "Project asset media type cannot exceed 200 characters.");
        if (byteLength <= 0) throw new ArgumentOutOfRangeException(nameof(byteLength), "Project asset byte length must be positive.");
        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64 || sha256.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Project asset SHA-256 must contain exactly 64 hexadecimal characters.", nameof(sha256));
        if (createdUtc == default) throw new ArgumentException("A project asset creation time is required.", nameof(createdUtc));
        var normalizedName = NormalizeName(name);

        Id = id;
        Kind = kind;
        MediaType = mediaType.Trim().ToLowerInvariant();
        ByteLength = byteLength;
        Sha256 = sha256.ToLowerInvariant();
        CreatedUtc = createdUtc;
        Name = normalizedName;
    }

    public ProjectAssetId Id { get; }
    public ProjectAssetKind Kind { get; }
    public string MediaType { get; }
    public long ByteLength { get; }
    public string Sha256 { get; }
    public DateTimeOffset CreatedUtc { get; }
    public string Name { get; }

    public ProjectAsset Rename(string name) => new(
        Id,
        Kind,
        MediaType,
        ByteLength,
        Sha256,
        CreatedUtc,
        name);

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A project asset name is required.", nameof(name));
        var normalized = name.Trim();
        if (normalized.Length > 80) throw new ArgumentOutOfRangeException(nameof(name), "A project asset name cannot exceed 80 characters.");
        return normalized;
    }
}
