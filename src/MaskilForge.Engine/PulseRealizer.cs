using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record PulseProposalEvent(
    RegisteredPitch Pitch,
    long StartTick,
    long DurationTicks,
    int Velocity,
    NoteEventId SourceNoteEventId,
    NoteEventId? ExistingNoteEventId);

/// <summary>A transient, reviewable role realization. It is not project data until accepted.</summary>
public sealed record PulseProposal(
    SectionId SectionId,
    string PartLabel,
    IReadOnlyList<PulseProposalEvent> Events,
    int ReusedNoteCount);

public static class PulseRealizer
{
    private static readonly RegisteredPitch PulsePitch = new(NoteLetter.C, Accidental.Natural, 3);

    public static PulseProposal Propose(SongProject project, SectionId sectionId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var section = project.FindSection(sectionId);
        if (project.FindSectionRole(sectionId, ArrangementRole.Pulse) is null)
            throw new InvalidOperationException("Choose Pulse for this section before exploring a part idea.");
        if (project.MusicalParts.Any(item => item.SectionId == sectionId && item.Role == ArrangementRole.Pulse))
            throw new InvalidOperationException("This section already has a pulse part. Remove it before exploring another idea.");

        var placement = project.Timeline.FindSection(sectionId);
        var sectionStart = project.Timeline.ToAbsoluteTicks(placement.Start);
        var meter = project.TimeSignature;
        var ticksPerBeat = checked(project.Timeline.TicksPerQuarterNote * 4 / meter.Denominator);
        var sectionEnd = checked(sectionStart + (long)placement.DurationBars * meter.Numerator * ticksPerBeat);
        var pulseDuration = Math.Max(1, ticksPerBeat / 4);
        var sourceNotes = project.NoteEvents
            .Where(item => item.StartTick >= sectionStart && item.StartTick < sectionEnd)
            .GroupBy(item => item.StartTick)
            .Select(group => group.OrderBy(item => item.Pitch.MidiNumber).ThenBy(item => item.Id.Value).First())
            .OrderBy(item => item.StartTick)
            .ToList();
        if (sourceNotes.Count == 0)
            throw new InvalidOperationException("Approve playable notes in this section before exploring pulse.");

        var events = sourceNotes.Select(source =>
        {
            var existing = project.NoteEvents.FirstOrDefault(item =>
                item.StartTick == source.StartTick && item.Pitch.MidiNumber == PulsePitch.MidiNumber);
            return new PulseProposalEvent(
                PulsePitch,
                source.StartTick,
                pulseDuration,
                source.Velocity,
                source.Id,
                existing?.Id);
        }).ToList();

        return new PulseProposal(
            sectionId,
            $"{section.Title} pulse",
            events,
            events.Count(item => item.ExistingNoteEventId is not null));
    }
}
