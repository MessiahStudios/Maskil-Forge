namespace MaskilForge.Domain;

public enum VocalProductionDescriptor
{
    Clean,
    Warm,
    Intimate,
    Forward,
    SoftRock,
    Cinematic,
    Aggressive
}

public sealed record VocalProductionIntent
{
    public const int MaximumDescriptorCount = 4;
    public const int MaximumArtistNotesLength = 500;

    public VocalProductionIntent(
        IReadOnlyList<VocalProductionDescriptor> descriptors,
        string artistNotes,
        DateTimeOffset updatedUtc)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        if (descriptors.Any(descriptor => !Enum.IsDefined(descriptor)))
            throw new ArgumentException("Choose a supported desired vocal result.", nameof(descriptors));
        var normalizedDescriptors = descriptors.Distinct().ToArray();
        if (normalizedDescriptors.Length == 0)
            throw new ArgumentException("Choose at least one desired vocal result.", nameof(descriptors));
        if (normalizedDescriptors.Length > MaximumDescriptorCount)
            throw new ArgumentException($"Choose no more than {MaximumDescriptorCount} desired vocal results.", nameof(descriptors));
        if (updatedUtc == default) throw new ArgumentException("An update time is required.", nameof(updatedUtc));

        var normalizedNotes = (artistNotes ?? string.Empty).Trim();
        if (normalizedNotes.Length > MaximumArtistNotesLength)
            throw new ArgumentException($"Vocal-direction notes must be {MaximumArtistNotesLength} characters or fewer.", nameof(artistNotes));

        Descriptors = Array.AsReadOnly(normalizedDescriptors);
        ArtistNotes = normalizedNotes;
        UpdatedUtc = updatedUtc;
    }

    public IReadOnlyList<VocalProductionDescriptor> Descriptors { get; }
    public string ArtistNotes { get; }
    public DateTimeOffset UpdatedUtc { get; }
}
