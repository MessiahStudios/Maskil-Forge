using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record AccentProposalEvent(
    RegisteredPitch Pitch,
    long StartTick,
    long DurationTicks,
    int Velocity,
    NoteEventId SourceNoteEventId,
    NoteEventId? ExistingNoteEventId);

/// <summary>A transient, reviewable role realization. It is not project data until accepted.</summary>
public sealed record AccentProposal(
    SectionId SectionId,
    string PartLabel,
    IReadOnlyList<AccentProposalEvent> Events,
    int ReusedNoteCount);

public static class AccentRealizer
{
    private const int AccentVelocity = 112;

    public static AccentProposal Propose(SongProject project, SectionId sectionId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var section = project.FindSection(sectionId);
        if (project.FindSectionRole(sectionId, ArrangementRole.Accent) is null)
            throw new InvalidOperationException("Choose Accents for this section before exploring a part idea.");
        if (project.MusicalParts.Any(item => item.SectionId == sectionId && item.Role == ArrangementRole.Accent))
            throw new InvalidOperationException("This section already has an accents part. Remove it before exploring another idea.");

        var placement = project.Timeline.FindSection(sectionId);
        var sectionStart = project.Timeline.ToAbsoluteTicks(placement.Start);
        var meter = project.TimeSignature;
        var ticksPerBeat = checked(project.Timeline.TicksPerQuarterNote * 4 / meter.Denominator);
        var ticksPerBar = checked((long)meter.Numerator * ticksPerBeat);
        var sectionEnd = checked(sectionStart + (long)placement.DurationBars * ticksPerBar);
        var accentDuration = Math.Max(1, ticksPerBeat / 4);
        var sectionNotes = project.NoteEvents
            .Where(item => item.StartTick >= sectionStart && item.StartTick < sectionEnd)
            .ToList();
        if (sectionNotes.Count == 0)
            throw new InvalidOperationException("Approve playable notes in this section before exploring accents.");

        var sourceNotes = sectionNotes
            .Where(item => (item.StartTick - sectionStart) % ticksPerBar == 0)
            .GroupBy(item => item.StartTick)
            .Select(group => group.OrderByDescending(item => item.Pitch.MidiNumber).ThenBy(item => item.Id.Value).First())
            .OrderBy(item => item.StartTick)
            .ToList();
        if (sourceNotes.Count == 0)
            throw new InvalidOperationException("Approve at least one note on a bar downbeat so accents can mark important moments.");

        var events = sourceNotes.Select(source =>
        {
            var existing = project.NoteEvents.FirstOrDefault(item =>
                item.StartTick == source.StartTick
                && item.DurationTicks == accentDuration
                && item.Pitch.MidiNumber == source.Pitch.MidiNumber
                && item.Velocity == AccentVelocity);
            return new AccentProposalEvent(
                source.Pitch,
                source.StartTick,
                accentDuration,
                AccentVelocity,
                source.Id,
                existing?.Id);
        }).ToList();

        return new AccentProposal(
            sectionId,
            $"{section.Title} accents",
            events,
            events.Count(item => item.ExistingNoteEventId is not null));
    }
}
