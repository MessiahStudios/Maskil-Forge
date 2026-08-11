using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record HarmonySupportProposalEvent(
    RegisteredPitch Pitch,
    long StartTick,
    long DurationTicks,
    int Velocity,
    bool UsesPreviewVoicing,
    NoteEventId? ExistingNoteEventId);

/// <summary>A transient, reviewable role realization. It is not project data until accepted.</summary>
public sealed record HarmonySupportProposal(
    SectionId SectionId,
    string PartLabel,
    IReadOnlyList<HarmonySupportProposalEvent> Events,
    int ReusedNoteCount,
    bool UsesPreviewVoicings);

public static class HarmonySupportRealizer
{
    public static HarmonySupportProposal Propose(SongProject project, SectionId sectionId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var section = project.FindSection(sectionId);
        if (project.FindSectionRole(sectionId, ArrangementRole.Harmony) is null)
            throw new InvalidOperationException("Choose Harmony support for this section before exploring a part idea.");
        if (project.MusicalParts.Any(item => item.SectionId == sectionId && item.Role == ArrangementRole.Harmony))
            throw new InvalidOperationException("This section already has a harmony support part. Remove it before exploring another idea.");

        var sketch = HarmonyNoteSketcher.Project(project, sectionId);
        var events = sketch.Events.Select(item =>
        {
            var existing = project.NoteEvents.FirstOrDefault(note =>
                note.StartTick == item.StartTick
                && note.DurationTicks == item.DurationTicks
                && note.Pitch.MidiNumber == item.Pitch.MidiNumber);
            return new HarmonySupportProposalEvent(
                item.Pitch,
                item.StartTick,
                item.DurationTicks,
                item.Velocity,
                item.UsesPreviewVoicing,
                existing?.Id);
        }).ToList();

        return new HarmonySupportProposal(
            sectionId,
            $"{section.Title} harmony support",
            events,
            events.Count(item => item.ExistingNoteEventId is not null),
            sketch.UsesPreviewVoicings);
    }
}
