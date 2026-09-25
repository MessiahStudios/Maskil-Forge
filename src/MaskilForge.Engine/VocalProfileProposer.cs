using MaskilForge.Domain;
using System.Security.Cryptography;
using System.Text.Json;

namespace MaskilForge.Engine;

public sealed record VocalProfileJob(VocalProcessingRole Role, string Name, IReadOnlyList<string> Reasons, bool CanPreview);
public sealed record VocalProfileProposal(string SourceSignature, IReadOnlyList<VocalProfileJob> Jobs,
    IReadOnlyList<VocalProcessingRole> CurrentRoles, IReadOnlyList<VocalProcessingRole> AddedRoles,
    IReadOnlyList<VocalProcessingRole> RemovedRoles, bool OrderChanges, bool HasChanges);

/// <summary>Deterministic host suggestions from artist-selected language, never analyzer evidence or audio settings.</summary>
public static class VocalProfileProposer
{
    public const int Version = 1;

    public static string SourceSignature(SongProject project) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        Version, project.Id, project.VocalProductionIntent, project.VocalProcessingChain,
        project.VocalProcessingRecipes, project.Assets
    })));

    public static VocalProfileProposal Propose(SongProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var intent = project.VocalProductionIntent
            ?? throw new InvalidOperationException("Set a vocal direction before requesting production suggestions.");
        var reasons = new Dictionary<VocalProcessingRole, List<string>>();
        void Suggest(VocalProcessingRole role, string reason)
        {
            if (!reasons.TryGetValue(role, out var list)) reasons[role] = list = [];
            list.Add(reason);
        }
        // Stable descriptor and role order makes combinations independent of checkbox selection order.
        foreach (var descriptor in intent.Descriptors.Order())
        {
            switch (descriptor)
            {
                case VocalProductionDescriptor.Clean:
                    Suggest(VocalProcessingRole.CorrectiveTone, "Clean: consider reducing distracting low-frequency buildup; listen for loss of vocal body.");
                    Suggest(VocalProcessingRole.TransparentDynamics, "Clean: consider keeping words at a consistent level while preserving their character.");
                    break;
                case VocalProductionDescriptor.Warm:
                    Suggest(VocalProcessingRole.CorrectiveTone, "Warm: audition whether reducing rumble leaves room for the voice's body; reject it if the voice becomes thin.");
                    Suggest(VocalProcessingRole.Saturation, "Warm: consider gentle tonal color and density while keeping the words clear.");
                    break;
                case VocalProductionDescriptor.Intimate:
                    Suggest(VocalProcessingRole.TransparentDynamics, "Intimate: consider gentle level control so quiet words remain close without erasing breath or phrasing.");
                    Suggest(VocalProcessingRole.Space, "Intimate: consider a restrained sense of depth so the performance stays close.");
                    break;
                case VocalProductionDescriptor.Forward:
                    Suggest(VocalProcessingRole.CorrectiveTone, "Forward: consider reducing distracting tonal buildup without making the voice smaller.");
                    Suggest(VocalProcessingRole.TransparentDynamics, "Forward: consider consistent word levels to help the voice remain present.");
                    break;
                case VocalProductionDescriptor.SoftRock:
                    Suggest(VocalProcessingRole.CharacterCompression, "Soft Rock: consider shaping vocal energy while retaining the performance's movement.");
                    Suggest(VocalProcessingRole.Saturation, "Soft Rock: consider tonal color that supports the band's energy.");
                    Suggest(VocalProcessingRole.Space, "Soft Rock: consider depth around the voice while keeping the words defined.");
                    break;
                case VocalProductionDescriptor.Cinematic:
                    Suggest(VocalProcessingRole.TransparentDynamics, "Cinematic: consider level control that keeps words intelligible while preserving dramatic contrast.");
                    Suggest(VocalProcessingRole.Space, "Cinematic: consider a larger sense of depth; balance it against any request for intimacy.");
                    break;
                case VocalProductionDescriptor.Aggressive:
                    Suggest(VocalProcessingRole.CharacterCompression, "Aggressive: consider shaping urgency through dynamics without replacing the performance.");
                    Suggest(VocalProcessingRole.Saturation, "Aggressive: consider added tonal density while listening for loss of clarity.");
                    break;
            }
        }
        if (project.VocalProcessingRecipes.Any(item => item.Role == VocalProcessingRole.CorrectiveTone))
            Suggest(VocalProcessingRole.CorrectiveTone, "Keep Corrective Tone because a take already has accepted low-cut settings. Those settings remain unchanged.");
        if (project.VocalProcessingRecipes.Any(item => item.Role == VocalProcessingRole.TransparentDynamics))
            Suggest(VocalProcessingRole.TransparentDynamics, "Keep Transparent Level Control because a take already has accepted level-control settings. Those settings remain unchanged.");
        if (project.VocalProcessingRecipes.Any(item => item.Role == VocalProcessingRole.Saturation))
            Suggest(VocalProcessingRole.Saturation, "Keep Saturation / Color because a take already has accepted saturation settings. Those settings remain unchanged.");
        if (project.VocalProcessingRecipes.Any(item => item.Role == VocalProcessingRole.CharacterCompression))
            Suggest(VocalProcessingRole.CharacterCompression, "Keep Character Compression because a take already has accepted character-compression settings. Those settings remain unchanged.");
        if (project.VocalProcessingRecipes.Any(item => item.Role == VocalProcessingRole.Cleanup))
            Suggest(VocalProcessingRole.Cleanup, "Keep Cleanup because a take already has accepted cleanup settings. Those settings remain unchanged.");
        var jobs = VocalProcessingRoleCatalog.Roles.Where(role => reasons.ContainsKey(role.Id))
            .Select(role => new VocalProfileJob(role.Id, role.Name, reasons[role.Id].AsReadOnly(), role.Id == VocalProcessingRole.CorrectiveTone)).ToArray();
        var roles = jobs.Select(job => job.Role).ToArray();
        var current = project.VocalProcessingChain?.Roles.ToArray() ?? [];
        var retained = current.Intersect(roles).ToArray();
        return new(SourceSignature(project), Array.AsReadOnly(jobs), Array.AsReadOnly(current),
            Array.AsReadOnly(roles.Except(current).ToArray()), Array.AsReadOnly(current.Except(roles).ToArray()),
            !retained.SequenceEqual(roles.Where(retained.Contains)), !current.SequenceEqual(roles));
    }
}

public sealed class AcceptVocalProfileProposalCommand(string sourceSignature) : IProjectCommand
{
    private SetVocalProcessingChainCommand? _change;
    public void Execute(SongProject project)
    {
        if (_change is not null) { _change.Execute(project); return; }
        if (VocalProfileProposer.SourceSignature(project) != sourceSignature)
            throw new InvalidOperationException("The song changed since this proposal was prepared. Preview the suggestions again.");
        var proposal = VocalProfileProposer.Propose(project);
        if (!proposal.HasChanges) throw new InvalidOperationException("These production jobs already match the current plan.");
        var change = new SetVocalProcessingChainCommand(proposal.Jobs.Select(job => job.Role).ToArray());
        change.Execute(project);
        _change = change;
    }
    public void Undo(SongProject project) => (_change ?? throw new InvalidOperationException("Command has not been executed.")).Undo(project);
}
