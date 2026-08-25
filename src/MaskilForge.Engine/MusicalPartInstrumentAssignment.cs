using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// Resolves an artist-chosen catalog instrument for a musical part. Empty means
/// unassigned. Unknown slugs are rejected. The helper does not recommend, rank,
/// retarget, or emit MIDI.
/// </summary>
public static class MusicalPartInstrumentAssignment
{
    public static string? RequireCatalogId(string? instrumentProfileId, InstrumentProfileCatalog? catalog = null)
    {
        if (string.IsNullOrWhiteSpace(instrumentProfileId)) return null;
        var id = instrumentProfileId.Trim();
        if (!InstrumentProfile.IsValidId(id))
            throw new ArgumentException("An assigned instrument must be a catalog slug of at most 40 characters.", nameof(instrumentProfileId));
        catalog ??= InstrumentProfileCatalogLoader.Current;
        if (!catalog.Instruments.Any(item => string.Equals(item.Id, id, StringComparison.Ordinal)))
            throw new ArgumentException($"Instrument profile '{id}' was not found.", nameof(instrumentProfileId));
        return id;
    }
}
