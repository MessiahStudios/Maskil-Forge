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

public enum ArrangementRole
{
    Foundation,
    Pulse,
    Harmony,
    LowEndSupport,
    Texture,
    Accent,
    Transition,
    Countermelody,
    HookReinforcement
}

/// <summary>An artist's decision that one musical job belongs in one song section.</summary>
public sealed class SectionRoleAssignment
{
    [JsonConstructor]
    public SectionRoleAssignment(
        SectionRoleAssignmentId id,
        SectionId sectionId,
        ArrangementRole role,
        ArrangementProvenance provenance)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A role-assignment ID is required.", nameof(id));
        if (sectionId.Value == Guid.Empty) throw new ArgumentException("A section ID is required.", nameof(sectionId));
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role), "Arrangement role is invalid.");
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance), "Arrangement provenance is invalid.");
        Id = id;
        SectionId = sectionId;
        Role = role;
        Provenance = provenance;
    }

    public SectionRoleAssignmentId Id { get; }
    public SectionId SectionId { get; }
    public ArrangementRole Role { get; }
    public ArrangementProvenance Provenance { get; }
}

/// <summary>
/// An artist-authored grouping that explains which approved notes fulfill one
/// arrangement role in one section. It does not choose an instrument or generate notes.
/// </summary>
public sealed class MusicalPart
{
    [JsonConstructor]
    public MusicalPart(
        MusicalPartId id,
        SectionId sectionId,
        ArrangementRole role,
        string label,
        IReadOnlyList<NoteEventId> noteEventIds,
        ArrangementProvenance provenance)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A musical-part ID is required.", nameof(id));
        if (sectionId.Value == Guid.Empty) throw new ArgumentException("A section ID is required.", nameof(sectionId));
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role), "Arrangement role is invalid.");
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A musical-part name is required.", nameof(label));
        if (label.Trim().Length > 100) throw new ArgumentOutOfRangeException(nameof(label), "A musical-part name cannot exceed 100 characters.");
        ArgumentNullException.ThrowIfNull(noteEventIds);
        if (noteEventIds.Count == 0) throw new ArgumentException("A musical part must reference at least one approved note.", nameof(noteEventIds));
        if (noteEventIds.Any(item => item.Value == Guid.Empty) || noteEventIds.Distinct().Count() != noteEventIds.Count)
            throw new ArgumentException("Musical-part note references must be valid and unique.", nameof(noteEventIds));
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance), "Arrangement provenance is invalid.");
        Id = id;
        SectionId = sectionId;
        Role = role;
        Label = label.Trim();
        NoteEventIds = noteEventIds.ToList();
        Provenance = provenance;
    }

    public MusicalPartId Id { get; }
    public SectionId SectionId { get; }
    public ArrangementRole Role { get; }
    public string Label { get; }
    public IReadOnlyList<NoteEventId> NoteEventIds { get; }
    public ArrangementProvenance Provenance { get; }
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
