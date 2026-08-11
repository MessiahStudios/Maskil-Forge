using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record HookReinforcementProposalEvent(
    RegisteredPitch Pitch,
    long StartTick,
    long DurationTicks,
    int Velocity,
    NoteEventId SourceNoteEventId,
    NoteEventId? ExistingNoteEventId);

/// <summary>A transient, reviewable role realization. It is not project data until accepted.</summary>
public sealed record HookReinforcementProposal(
    SectionId SectionId,
    string PartLabel,
    IReadOnlyList<HookReinforcementProposalEvent> Events,
    int ReusedNoteCount);

public static class HookReinforcementRealizer
{
    private const int AccentVelocity = 108;

    public static HookReinforcementProposal Propose(SongProject project, SectionId sectionId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var section = project.FindSection(sectionId);
        if (project.FindSectionRole(sectionId, ArrangementRole.HookReinforcement) is null)
            throw new InvalidOperationException("Choose Hook reinforcement for this section before exploring a part idea.");
        if (project.MusicalParts.Any(item => item.SectionId == sectionId && item.Role == ArrangementRole.HookReinforcement))
            throw new InvalidOperationException("This section already has a hook reinforcement part. Remove it before exploring another idea.");

        var placement = project.Timeline.FindSection(sectionId);
        var sectionStart = project.Timeline.ToAbsoluteTicks(placement.Start);
        var meter = project.TimeSignature;
        var ticksPerBeat = checked(project.Timeline.TicksPerQuarterNote * 4 / meter.Denominator);
        var sectionEnd = checked(sectionStart + (long)placement.DurationBars * meter.Numerator * ticksPerBeat);
        var sourceNotes = project.NoteEvents
            .Where(item => item.StartTick >= sectionStart && item.StartTick < sectionEnd)
            .GroupBy(item => item.StartTick)
            .Select(group => group.OrderByDescending(item => item.Pitch.MidiNumber).ThenBy(item => item.Id.Value).First())
            .OrderBy(item => item.StartTick)
            .ToList();
        if (sourceNotes.Count == 0)
            throw new InvalidOperationException("Approve playable notes in this section before exploring hook reinforcement.");

        var events = sourceNotes.Select(source =>
        {
            var duration = Math.Max(1, Math.Min(source.DurationTicks, ticksPerBeat));
            var velocity = Math.Min(127, Math.Max(source.Velocity, AccentVelocity));
            var existing = project.NoteEvents.FirstOrDefault(item =>
                item.StartTick == source.StartTick
                && item.DurationTicks == duration
                && item.Pitch.MidiNumber == source.Pitch.MidiNumber);
            return new HookReinforcementProposalEvent(
                source.Pitch,
                source.StartTick,
                duration,
                velocity,
                source.Id,
                existing?.Id);
        }).ToList();

        return new HookReinforcementProposal(
            sectionId,
            $"{section.Title} hook reinforcement",
            events,
            events.Count(item => item.ExistingNoteEventId is not null));
    }
}
