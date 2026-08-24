using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MaskilForge.Domain;

public enum InstrumentArticulation
{
    Legato,
    BowExpression,
    Slide,
    Picking,
    Bend,
    HammerOn
}

public enum InstrumentExpressiveQuality
{
    Warm,
    Bright,
    Intimate,
    Sustained,
    Percussive,
    Agile
}

/// <summary>
/// Host-owned knowledge about one instrument. It is not a song assignment, renderer
/// mapping, or retargeted performance.
/// </summary>
public sealed class InstrumentProfile
{
    private static readonly Regex IdPattern = new("^[a-z][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [JsonConstructor]
    public InstrumentProfile(
        string id,
        string name,
        RegisteredPitch minimumPitch,
        RegisteredPitch maximumPitch,
        IReadOnlyList<ArrangementRole> roles,
        IReadOnlyList<InstrumentArticulation> articulations,
        IReadOnlyList<InstrumentExpressiveQuality> expressiveQualities)
    {
        if (string.IsNullOrWhiteSpace(id) || !IdPattern.IsMatch(id))
            throw new ArgumentException("An instrument profile ID must be a lowercase slug of at most 40 characters.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An instrument profile name is required.", nameof(name));
        var normalizedName = name.Trim();
        if (normalizedName.Length > 80) throw new ArgumentOutOfRangeException(nameof(name), "An instrument profile name cannot exceed 80 characters.");
        ArgumentNullException.ThrowIfNull(minimumPitch);
        ArgumentNullException.ThrowIfNull(maximumPitch);
        if (minimumPitch.MidiNumber > maximumPitch.MidiNumber)
            throw new ArgumentException("An instrument profile maximum pitch cannot sit below its minimum pitch.", nameof(maximumPitch));
        ArgumentNullException.ThrowIfNull(roles);
        if (roles.Count == 0) throw new ArgumentException("An instrument profile must name at least one arrangement role.", nameof(roles));
        if (roles.Any(role => !Enum.IsDefined(role))) throw new ArgumentOutOfRangeException(nameof(roles), "Instrument-profile role is invalid.");
        if (roles.Distinct().Count() != roles.Count) throw new ArgumentException("Instrument-profile roles must be unique.", nameof(roles));
        ArgumentNullException.ThrowIfNull(articulations);
        if (articulations.Count == 0) throw new ArgumentException("An instrument profile must name at least one articulation.", nameof(articulations));
        if (articulations.Any(item => !Enum.IsDefined(item)))
            throw new ArgumentOutOfRangeException(nameof(articulations), "Instrument-profile articulation is invalid.");
        if (articulations.Distinct().Count() != articulations.Count)
            throw new ArgumentException("Instrument-profile articulations must be unique.", nameof(articulations));
        ArgumentNullException.ThrowIfNull(expressiveQualities);
        if (expressiveQualities.Count == 0)
            throw new ArgumentException("An instrument profile must name at least one expressive quality.", nameof(expressiveQualities));
        if (expressiveQualities.Any(item => !Enum.IsDefined(item)))
            throw new ArgumentOutOfRangeException(nameof(expressiveQualities), "Instrument-profile expressive quality is invalid.");
        if (expressiveQualities.Distinct().Count() != expressiveQualities.Count)
            throw new ArgumentException("Instrument-profile expressive qualities must be unique.", nameof(expressiveQualities));

        Id = id;
        Name = normalizedName;
        MinimumPitch = minimumPitch;
        MaximumPitch = maximumPitch;
        Roles = roles.ToList();
        Articulations = articulations.ToList();
        ExpressiveQualities = expressiveQualities.ToList();
    }

    public string Id { get; }
    public string Name { get; }
    public RegisteredPitch MinimumPitch { get; }
    public RegisteredPitch MaximumPitch { get; }
    public IReadOnlyList<ArrangementRole> Roles { get; }
    public IReadOnlyList<InstrumentArticulation> Articulations { get; }
    public IReadOnlyList<InstrumentExpressiveQuality> ExpressiveQualities { get; }
}

public sealed class InstrumentProfileCatalog
{
    [JsonConstructor]
    public InstrumentProfileCatalog(int version, IReadOnlyList<InstrumentProfile> instruments)
    {
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version), "Instrument-profile catalog version must be at least 1.");
        ArgumentNullException.ThrowIfNull(instruments);
        if (instruments.Count == 0) throw new ArgumentException("An instrument-profile catalog must contain at least one instrument.", nameof(instruments));
        if (instruments.Select(item => item.Id).Distinct().Count() != instruments.Count)
            throw new ArgumentException("Instrument-profile IDs must be unique.", nameof(instruments));

        Version = version;
        Instruments = instruments.ToList();
    }

    public int Version { get; }
    public IReadOnlyList<InstrumentProfile> Instruments { get; }

    public InstrumentProfile Find(string id) =>
        Instruments.SingleOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"Instrument profile '{id}' was not found.");
}
