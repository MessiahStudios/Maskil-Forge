using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record CountermelodyProposalEvent(
    RegisteredPitch Pitch,
    long StartTick,
    long DurationTicks,
    int Velocity,
    NoteEventId SourceNoteEventId,
    NoteEventId? ExistingNoteEventId);

/// <summary>A transient, reviewable role realization. It is not project data until accepted.</summary>
public sealed record CountermelodyProposal(
    SectionId SectionId,
    string PartLabel,
    IReadOnlyList<CountermelodyProposalEvent> Events,
    int ReusedNoteCount);

public static class CountermelodyRealizer
{
    private const int ResponseVelocity = 84;

    public static CountermelodyProposal Propose(SongProject project, SectionId sectionId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var section = project.FindSection(sectionId);
        if (project.FindSectionRole(sectionId, ArrangementRole.Countermelody) is null)
            throw new InvalidOperationException("Choose Countermelody for this section before exploring a part idea.");
        if (project.MusicalParts.Any(item => item.SectionId == sectionId && item.Role == ArrangementRole.Countermelody))
            throw new InvalidOperationException("This section already has a countermelody part. Remove it before exploring another idea.");

        var placement = project.Timeline.FindSection(sectionId);
        var sectionStart = project.Timeline.ToAbsoluteTicks(placement.Start);
        var meter = project.TimeSignature;
        var ticksPerBeat = checked(project.Timeline.TicksPerQuarterNote * 4 / meter.Denominator);
        var sectionEnd = checked(sectionStart + (long)placement.DurationBars * meter.Numerator * ticksPerBeat);
        var sectionNotes = project.NoteEvents
            .Where(item => item.StartTick >= sectionStart && item.StartTick < sectionEnd)
            .ToList();
        if (sectionNotes.Count == 0)
            throw new InvalidOperationException("Approve playable notes in this section before exploring countermelody.");
        var sourceNotes = sectionNotes
            .GroupBy(item => item.StartTick)
            .Where(group => group.Count() >= 2)
            .Select(group => group.OrderByDescending(item => item.Pitch.MidiNumber).ThenBy(item => item.Id.Value).Skip(1).First())
            .OrderBy(item => item.StartTick)
            .ToList();
        if (sourceNotes.Count == 0)
            throw new InvalidOperationException("Approve at least two notes at one musical moment so a supporting response can sit beneath the top line.");

        var events = sourceNotes.Select(source =>
        {
            var existing = project.NoteEvents.FirstOrDefault(item =>
                item.StartTick == source.StartTick
                && item.DurationTicks == source.DurationTicks
                && item.Pitch.MidiNumber == source.Pitch.MidiNumber
                && item.Velocity == ResponseVelocity);
            return new CountermelodyProposalEvent(
                source.Pitch,
                source.StartTick,
                source.DurationTicks,
                ResponseVelocity,
                source.Id,
                existing?.Id);
        }).ToList();

        return new CountermelodyProposal(
            sectionId,
            $"{section.Title} countermelody",
            events,
            events.Count(item => item.ExistingNoteEventId is not null));
    }
}
