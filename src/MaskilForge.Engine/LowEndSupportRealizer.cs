using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record LowEndSupportProposalEvent(
    RegisteredPitch Pitch,
    long StartTick,
    long DurationTicks,
    int Velocity,
    NoteEventId SourceNoteEventId,
    NoteEventId? ExistingNoteEventId);

/// <summary>A transient, reviewable role realization. It is not project data until accepted.</summary>
public sealed record LowEndSupportProposal(
    SectionId SectionId,
    string PartLabel,
    IReadOnlyList<LowEndSupportProposalEvent> Events,
    int ReusedNoteCount);

public static class LowEndSupportRealizer
{
    private const int HighestLowRegisterMidiNote = 47;

    public static LowEndSupportProposal Propose(SongProject project, SectionId sectionId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var section = project.FindSection(sectionId);
        if (project.FindSectionRole(sectionId, ArrangementRole.LowEndSupport) is null)
            throw new InvalidOperationException("Choose Low-end support for this section before exploring a part idea.");
        if (project.MusicalParts.Any(item => item.SectionId == sectionId && item.Role == ArrangementRole.LowEndSupport))
            throw new InvalidOperationException("This section already has a low-end support part. Remove it before exploring another idea.");

        var placement = project.Timeline.FindSection(sectionId);
        var sectionStart = project.Timeline.ToAbsoluteTicks(placement.Start);
        var meter = project.TimeSignature;
        var ticksPerBeat = checked(project.Timeline.TicksPerQuarterNote * 4 / meter.Denominator);
        var sectionEnd = checked(sectionStart + (long)placement.DurationBars * meter.Numerator * ticksPerBeat);
        var sourceNotes = project.NoteEvents
            .Where(item => item.StartTick >= sectionStart && item.StartTick < sectionEnd)
            .GroupBy(item => item.StartTick)
            .Select(group => group.OrderBy(item => item.Pitch.MidiNumber).ThenBy(item => item.Id.Value).First())
            .OrderBy(item => item.StartTick)
            .ToList();
        if (sourceNotes.Count == 0)
            throw new InvalidOperationException("Approve playable notes in this section before exploring low-end support.");

        var events = sourceNotes.Select(source =>
        {
            var targetMidi = source.Pitch.MidiNumber;
            while (targetMidi > HighestLowRegisterMidiNote) targetMidi -= 12;
            var existing = targetMidi == source.Pitch.MidiNumber ? source.Id : (NoteEventId?)null;
            return new LowEndSupportProposalEvent(
                FromMidiNumber(targetMidi),
                source.StartTick,
                source.DurationTicks,
                source.Velocity,
                source.Id,
                existing);
        }).ToList();

        return new LowEndSupportProposal(
            sectionId,
            $"{section.Title} low-end support",
            events,
            events.Count(item => item.ExistingNoteEventId is not null));
    }

    private static RegisteredPitch FromMidiNumber(int midiNumber)
    {
        var octave = midiNumber / 12 - 1;
        return (midiNumber % 12) switch
        {
            0 => new RegisteredPitch(NoteLetter.C, Accidental.Natural, octave),
            1 => new RegisteredPitch(NoteLetter.C, Accidental.Sharp, octave),
            2 => new RegisteredPitch(NoteLetter.D, Accidental.Natural, octave),
            3 => new RegisteredPitch(NoteLetter.D, Accidental.Sharp, octave),
            4 => new RegisteredPitch(NoteLetter.E, Accidental.Natural, octave),
            5 => new RegisteredPitch(NoteLetter.F, Accidental.Natural, octave),
            6 => new RegisteredPitch(NoteLetter.F, Accidental.Sharp, octave),
            7 => new RegisteredPitch(NoteLetter.G, Accidental.Natural, octave),
            8 => new RegisteredPitch(NoteLetter.G, Accidental.Sharp, octave),
            9 => new RegisteredPitch(NoteLetter.A, Accidental.Natural, octave),
            10 => new RegisteredPitch(NoteLetter.A, Accidental.Sharp, octave),
            _ => new RegisteredPitch(NoteLetter.B, Accidental.Natural, octave),
        };
    }
}
