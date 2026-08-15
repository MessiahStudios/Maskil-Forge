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
        DateTimeOffset createdUtc)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A project asset ID is required.", nameof(id));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind), "Project asset kind is invalid.");
        if (string.IsNullOrWhiteSpace(mediaType)) throw new ArgumentException("A project asset media type is required.", nameof(mediaType));
        if (mediaType.Trim().Length > 200) throw new ArgumentOutOfRangeException(nameof(mediaType), "Project asset media type cannot exceed 200 characters.");
        if (byteLength <= 0) throw new ArgumentOutOfRangeException(nameof(byteLength), "Project asset byte length must be positive.");
        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64 || sha256.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Project asset SHA-256 must contain exactly 64 hexadecimal characters.", nameof(sha256));
        if (createdUtc == default) throw new ArgumentException("A project asset creation time is required.", nameof(createdUtc));

        Id = id;
        Kind = kind;
        MediaType = mediaType.Trim().ToLowerInvariant();
        ByteLength = byteLength;
        Sha256 = sha256.ToLowerInvariant();
        CreatedUtc = createdUtc;
    }

    public ProjectAssetId Id { get; }
    public ProjectAssetKind Kind { get; }
    public string MediaType { get; }
    public long ByteLength { get; }
    public string Sha256 { get; }
    public DateTimeOffset CreatedUtc { get; }
}
