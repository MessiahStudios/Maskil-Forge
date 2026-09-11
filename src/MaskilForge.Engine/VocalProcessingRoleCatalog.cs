using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record VocalProcessingRoleDefinition(VocalProcessingRole Id, string Name, string Purpose, string Technique);

/// <summary>Host-owned vocabulary, not a preset or a recommendation for any particular take.</summary>
public static class VocalProcessingRoleCatalog
{
    public const int Version = 1;

    public static IReadOnlyList<VocalProcessingRoleDefinition> Roles { get; } = Array.AsReadOnly<VocalProcessingRoleDefinition>([
        new(VocalProcessingRole.Cleanup, "Cleanup", "Reduce distractions between sung phrases.", "Gate / cleanup"),
        new(VocalProcessingRole.CorrectiveTone, "Corrective tone", "Address distracting tonal buildup while preserving the voice.", "Dynamic EQ / corrective tone"),
        new(VocalProcessingRole.CharacterCompression, "Character compression", "Shape the voice's energy and character through its dynamics.", "Character compression"),
        new(VocalProcessingRole.Saturation, "Saturation / color", "Add tonal color and density.", "Saturation"),
        new(VocalProcessingRole.TransparentDynamics, "Transparent level control", "Keep words at a more consistent level while preserving their character.", "Transparent dynamics control"),
        new(VocalProcessingRole.SibilanceControl, "Sibilance control", "Soften distracting sharp consonants.", "De-esser"),
        new(VocalProcessingRole.Space, "Space", "Describe the sense of depth around the voice.", "Reverb and delay")
    ]);
}
