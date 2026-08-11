using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record TextureProposalEvent(
    RegisteredPitch Pitch,
    long StartTick,
    long DurationTicks,
    int Velocity,
    bool UsesPreviewVoicing,
    NoteEventId? ExistingNoteEventId);

/// <summary>A transient, reviewable role realization. It is not project data until accepted.</summary>
public sealed record TextureProposal(
    SectionId SectionId,
    string PartLabel,
    IReadOnlyList<TextureProposalEvent> Events,
    int ReusedNoteCount,
    bool UsesPreviewVoicings);

public static class TextureRealizer
{
    private const int TextureVelocity = 72;

    public static TextureProposal Propose(SongProject project, SectionId sectionId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var section = project.FindSection(sectionId);
        if (project.FindSectionRole(sectionId, ArrangementRole.Texture) is null)
            throw new InvalidOperationException("Choose Texture for this section before exploring a part idea.");
        if (project.MusicalParts.Any(item => item.SectionId == sectionId && item.Role == ArrangementRole.Texture))
            throw new InvalidOperationException("This section already has a texture part. Remove it before exploring another idea.");

        var sketch = HarmonyNoteSketcher.Project(project, sectionId);
        var events = sketch.Events
            .GroupBy(item => item.StartTick)
            .SelectMany(group =>
            {
                var ordered = group.OrderByDescending(item => item.Pitch.MidiNumber).ThenBy(item => item.Pitch.ToDisplayString()).ToList();
                var keepCount = Math.Max(1, (ordered.Count + 1) / 2);
                return ordered.Take(keepCount);
            })
            .OrderBy(item => item.StartTick)
            .ThenBy(item => item.Pitch.MidiNumber)
            .Select(item =>
            {
                var existing = project.NoteEvents.FirstOrDefault(note =>
                    note.StartTick == item.StartTick
                    && note.DurationTicks == item.DurationTicks
                    && note.Pitch.MidiNumber == item.Pitch.MidiNumber);
                return new TextureProposalEvent(
                    item.Pitch,
                    item.StartTick,
                    item.DurationTicks,
                    TextureVelocity,
                    item.UsesPreviewVoicing,
                    existing?.Id);
            })
            .ToList();

        return new TextureProposal(
            sectionId,
            $"{section.Title} texture",
            events,
            events.Count(item => item.ExistingNoteEventId is not null),
            events.Any(item => item.UsesPreviewVoicing));
    }
}
