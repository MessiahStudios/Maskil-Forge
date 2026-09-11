namespace MaskilForge.Domain;

public enum VocalProcessingRole
{
    Cleanup,
    CorrectiveTone,
    CharacterCompression,
    Saturation,
    TransparentDynamics,
    SibilanceControl,
    Space
}

/// <summary>An artist-selected order of production jobs, independent of audio realization.</summary>
public sealed record VocalProcessingChain
{
    public VocalProcessingChain(IReadOnlyList<VocalProcessingRole> roles, DateTimeOffset updatedUtc)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (roles.Count == 0) throw new ArgumentException("Choose at least one production job.", nameof(roles));
        if (roles.Any(role => !Enum.IsDefined(role)))
            throw new ArgumentException("Choose a supported vocal production job.", nameof(roles));
        if (roles.Distinct().Count() != roles.Count)
            throw new ArgumentException("Each production job may appear only once in the chain.", nameof(roles));
        if (updatedUtc == default) throw new ArgumentException("An update time is required.", nameof(updatedUtc));
        Roles = Array.AsReadOnly(roles.ToArray());
        UpdatedUtc = updatedUtc;
    }

    public IReadOnlyList<VocalProcessingRole> Roles { get; }
    public DateTimeOffset UpdatedUtc { get; }
}
