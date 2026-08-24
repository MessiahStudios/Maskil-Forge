using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

/// <summary>
/// An artist-authored song-time start for one original-vocal take.
/// Absence means the take begins at song tick 0. Placement does not move
/// already-accepted notes, attach audio to the timeline, or create a DAW clip.
/// </summary>
public sealed record VocalTakePlacement
{
    [JsonConstructor]
    public VocalTakePlacement(
        VocalTakePlacementId id,
        ProjectAssetId assetId,
        MusicalPosition start,
        DateTimeOffset createdUtc,
        DateTimeOffset updatedUtc)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A vocal-take placement ID is required.", nameof(id));
        if (assetId.Value == Guid.Empty) throw new ArgumentException("A placed vocal-take asset ID is required.", nameof(assetId));
        if (createdUtc == default) throw new ArgumentException("A placement creation time is required.", nameof(createdUtc));
        if (updatedUtc == default) throw new ArgumentException("A placement update time is required.", nameof(updatedUtc));
        if (updatedUtc < createdUtc) throw new ArgumentOutOfRangeException(nameof(updatedUtc), "A placement cannot be updated before it was created.");

        Id = id;
        AssetId = assetId;
        Start = start;
        CreatedUtc = createdUtc;
        UpdatedUtc = updatedUtc;
    }

    public VocalTakePlacementId Id { get; }
    public ProjectAssetId AssetId { get; }
    public MusicalPosition Start { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset UpdatedUtc { get; }

    public VocalTakePlacement Relocate(MusicalPosition start, DateTimeOffset updatedUtc) =>
        new(Id, AssetId, start, CreatedUtc, updatedUtc);
}
