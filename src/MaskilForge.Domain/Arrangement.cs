using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum SectionEnergy
{
    Intimate = 1,
    Gentle = 2,
    Building = 3,
    Strong = 4,
    Peak = 5
}

public enum SectionDensity
{
    Sparse = 1,
    Light = 2,
    Balanced = 3,
    Full = 4,
    Dense = 5
}

public enum ArrangementProvenance
{
    Manual,
    Analyzer,
    Imported
}

/// <summary>An artist-authored arrangement intention for one existing song section.</summary>
public sealed class SectionArrangement
{
    [JsonConstructor]
    public SectionArrangement(
        SectionArrangementId id,
        SectionId sectionId,
        SectionEnergy energy,
        SectionDensity density,
        ArrangementProvenance provenance)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A section arrangement ID is required.", nameof(id));
        if (sectionId.Value == Guid.Empty) throw new ArgumentException("A section ID is required.", nameof(sectionId));
        if (!Enum.IsDefined(energy)) throw new ArgumentOutOfRangeException(nameof(energy), "Section energy is invalid.");
        if (!Enum.IsDefined(density)) throw new ArgumentOutOfRangeException(nameof(density), "Section density is invalid.");
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance), "Arrangement provenance is invalid.");
        Id = id;
        SectionId = sectionId;
        Energy = energy;
        Density = density;
        Provenance = provenance;
    }

    public SectionArrangementId Id { get; }
    public SectionId SectionId { get; }
    public SectionEnergy Energy { get; }
    public SectionDensity Density { get; }
    public ArrangementProvenance Provenance { get; }

    public SectionArrangement With(SectionEnergy energy, SectionDensity density) =>
        new(Id, SectionId, energy, density, Provenance);
}
