using MaskilForge.Domain;

namespace MaskilForge.Engine;

public interface IProjectCommand
{
    void Execute(SongProject project);
    void Undo(SongProject project);
}

public sealed class AddSectionCommand(SectionKind kind, string? title = null) : IProjectCommand
{
    private SongSection? _section;

    public SectionId? SectionId => _section?.Id;

    public void Execute(SongProject project)
    {
        _section ??= SongSection.Create(kind, title);
        project.InsertSection(project.Sections.Count, _section);
    }

    public void Undo(SongProject project)
    {
        if (_section is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveSection(_section.Id);
    }
}

public sealed class RenameSectionCommand(SectionId sectionId, string title) : IProjectCommand
{
    private string? _previousTitle;

    public void Execute(SongProject project)
    {
        var section = project.FindSection(sectionId);
        _previousTitle ??= section.Title;
        section.Rename(title);
    }

    public void Undo(SongProject project)
    {
        if (_previousTitle is null) throw new InvalidOperationException("Command has not been executed.");
        project.RenameSection(sectionId, _previousTitle);
    }
}

public sealed class ImportSongStructureCommand(IReadOnlyList<ProposedSongSection> proposals) : IProjectCommand
{
    private IReadOnlyList<SongSection>? _sections;
    private int? _startIndex;

    public void Execute(SongProject project)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        if (proposals.Count == 0) throw new ArgumentException("At least one proposed section is required.", nameof(proposals));
        if (project.MusicalParts.Count > 0)
            throw new InvalidOperationException("Import song structure before accepting musical parts so timeline timing stays explicit.");

        _startIndex ??= project.Sections.Count;
        _sections ??= proposals.Select(CreateSection).ToList();
        for (var index = 0; index < _sections.Count; index++)
            project.InsertSection(_startIndex.Value + index, _sections[index]);
    }

    public void Undo(SongProject project)
    {
        if (_sections is null) throw new InvalidOperationException("Command has not been executed.");
        foreach (var section in _sections.Reverse()) project.RemoveSection(section.Id);
    }

    private static SongSection CreateSection(ProposedSongSection proposal)
    {
        var section = SongSection.Create(proposal.Kind, proposal.Title);
        section.SetPerformanceIntent(proposal.Delivery, proposal.PerformanceNotes);
        section.SetStructuralFunction(proposal.StructuralFunction);
        foreach (var lyric in proposal.Lyrics.Where(line => !string.IsNullOrWhiteSpace(line))) section.AddLyricLine(lyric);
        return section;
    }
}

public sealed class SetSectionPerformanceIntentCommand(
    SectionId sectionId,
    SectionDelivery delivery,
    string performanceNotes) : IProjectCommand
{
    private SectionDelivery? _previousDelivery;
    private string? _previousNotes;

    public void Execute(SongProject project)
    {
        var section = project.FindSection(sectionId);
        _previousDelivery ??= section.Delivery;
        _previousNotes ??= section.PerformanceNotes;
        project.SetSectionPerformanceIntent(sectionId, delivery, performanceNotes);
    }

    public void Undo(SongProject project)
    {
        if (_previousDelivery is null || _previousNotes is null)
            throw new InvalidOperationException("Command has not been executed.");
        project.SetSectionPerformanceIntent(sectionId, _previousDelivery.Value, _previousNotes);
    }
}

public sealed class SetSectionStructuralFunctionCommand(SectionId sectionId, StructuralFunction structuralFunction) : IProjectCommand
{
    private StructuralFunction? _previous;

    public void Execute(SongProject project)
    {
        _previous ??= project.FindSection(sectionId).StructuralFunction;
        project.SetSectionStructuralFunction(sectionId, structuralFunction);
    }

    public void Undo(SongProject project)
    {
        if (_previous is null) throw new InvalidOperationException("Command has not been executed.");
        project.SetSectionStructuralFunction(sectionId, _previous.Value);
    }
}

public sealed class SetSectionIntentCommand(
    SectionId sectionId,
    StructuralFunction structuralFunction,
    SectionDelivery delivery,
    string performanceNotes) : IProjectCommand
{
    private StructuralFunction? _previousStructuralFunction;
    private SectionDelivery? _previousDelivery;
    private string? _previousNotes;

    public void Execute(SongProject project)
    {
        var section = project.FindSection(sectionId);
        _previousStructuralFunction ??= section.StructuralFunction;
        _previousDelivery ??= section.Delivery;
        _previousNotes ??= section.PerformanceNotes;
        project.SetSectionStructuralFunction(sectionId, structuralFunction);
        project.SetSectionPerformanceIntent(sectionId, delivery, performanceNotes);
    }

    public void Undo(SongProject project)
    {
        if (_previousStructuralFunction is null || _previousDelivery is null || _previousNotes is null)
            throw new InvalidOperationException("Command has not been executed.");
        project.SetSectionStructuralFunction(sectionId, _previousStructuralFunction.Value);
        project.SetSectionPerformanceIntent(sectionId, _previousDelivery.Value, _previousNotes);
    }
}

public sealed class DuplicateSectionCommand(SectionId sourceSectionId) : IProjectCommand
{
    private SongSection? _duplicate;
    private int? _insertIndex;
    private int? _durationBars;
    private SectionArrangement? _arrangement;
    private IReadOnlyList<SectionRoleAssignment>? _roles;

    public SectionId? DuplicateSectionId => _duplicate?.Id;

    public void Execute(SongProject project)
    {
        if (project.MusicalParts.Count > 0)
            throw new InvalidOperationException("Duplicate song sections before accepting musical parts. Remove the parts first so timeline timing stays explicit.");

        if (_duplicate is null)
        {
            var source = project.FindSection(sourceSectionId);
            _insertIndex = project.IndexOf(sourceSectionId) + 1;
            _durationBars = project.Timeline.FindSection(sourceSectionId).DurationBars;
            _duplicate = SongSection.Create(source.Kind, $"{source.Title} Copy");
            _duplicate.SetPerformanceIntent(source.Delivery, source.PerformanceNotes);
            _duplicate.SetStructuralFunction(source.StructuralFunction);
            foreach (var line in source.LyricLines) _duplicate.AddLyricLine(line.Text);

            project.InsertSection(_insertIndex.Value, _duplicate, _durationBars.Value);
            foreach (var sourceChord in source.Harmony)
            {
                var chord = project.AddHarmonyChord(
                    _duplicate.Id,
                    sourceChord.Chord,
                    sourceChord.Start,
                    sourceChord.DurationBars);
                if (sourceChord.Voicing is not null)
                    project.SetChordVoicing(
                        _duplicate.Id,
                        chord.Id,
                        sourceChord.Voicing.Voices.Select(voice => voice.Pitch).ToList(),
                        sourceChord.Voicing.MinimumMidiNote,
                        sourceChord.Voicing.MaximumMidiNote);
            }

            var sourceArrangement = project.FindSectionArrangement(sourceSectionId);
            if (sourceArrangement is not null)
                _arrangement = project.SetSectionArrangement(_duplicate.Id, sourceArrangement.Energy, sourceArrangement.Density);
            _roles = project.ArrangementRoles
                .Where(role => role.SectionId == sourceSectionId)
                .Select(role => project.SetSectionRole(_duplicate.Id, role.Role))
                .ToList();
            return;
        }

        project.InsertSection(_insertIndex!.Value, _duplicate, _durationBars!.Value);
        if (_arrangement is not null) project.RestoreSectionArrangement(_duplicate.Id, _arrangement);
        if (_roles is { Count: > 0 }) project.RestoreSectionRoles(_duplicate.Id, _roles);
    }

    public void Undo(SongProject project)
    {
        if (_duplicate is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveSection(_duplicate.Id);
    }
}

public sealed class ReuseSectionFoundationCommand(SectionId sourceSectionId, SectionId targetSectionId) : IProjectCommand
{
    private IReadOnlyList<HarmonyChord>? _previousHarmony;
    private SectionArrangement? _previousArrangement;
    private IReadOnlyList<SectionRoleAssignment>? _previousRoles;
    private IReadOnlyList<HarmonyChord>? _reusedHarmony;
    private SectionArrangement? _reusedArrangement;
    private IReadOnlyList<SectionRoleAssignment>? _reusedRoles;

    public void Execute(SongProject project)
    {
        if (sourceSectionId == targetSectionId)
            throw new InvalidOperationException("Choose a different section to reuse as a foundation.");
        if (project.MusicalParts.Any(part => part.SectionId == targetSectionId))
            throw new InvalidOperationException("Remove this section's musical parts before replacing its musical foundation.");

        var source = project.FindSection(sourceSectionId);
        project.FindSection(targetSectionId);
        if (_reusedHarmony is null)
        {
            _previousHarmony = project.FindSection(targetSectionId).Harmony.Select(HarmonyChordSnapshots.Clone).ToList();
            _previousArrangement = project.FindSectionArrangement(targetSectionId);
            _previousRoles = project.ArrangementRoles.Where(role => role.SectionId == targetSectionId).ToList();
            _reusedHarmony = source.Harmony.Select(CloneWithFreshIdentity).ToList();
            var sourceArrangement = project.FindSectionArrangement(sourceSectionId);
            _reusedArrangement = sourceArrangement is null ? null : new SectionArrangement(
                SectionArrangementId.New(), targetSectionId, sourceArrangement.Energy, sourceArrangement.Density, ArrangementProvenance.Manual);
            _reusedRoles = project.ArrangementRoles.Where(role => role.SectionId == sourceSectionId)
                .Select(role => new SectionRoleAssignment(SectionRoleAssignmentId.New(), targetSectionId, role.Role, ArrangementProvenance.Manual))
                .ToList();
        }

        project.ReplaceSectionHarmony(targetSectionId, _reusedHarmony.Select(HarmonyChordSnapshots.Clone));
        project.RestoreSectionArrangement(targetSectionId, _reusedArrangement);
        project.RestoreSectionRoles(targetSectionId, _reusedRoles!);
    }

    public void Undo(SongProject project)
    {
        if (_previousHarmony is null || _previousRoles is null)
            throw new InvalidOperationException("Command has not been executed.");
        project.ReplaceSectionHarmony(targetSectionId, _previousHarmony.Select(HarmonyChordSnapshots.Clone));
        project.RestoreSectionArrangement(targetSectionId, _previousArrangement);
        project.RestoreSectionRoles(targetSectionId, _previousRoles);
    }

    private static HarmonyChord CloneWithFreshIdentity(HarmonyChord chord)
    {
        var voicing = chord.Voicing is null ? null : new ChordVoicing(
            ChordVoicingId.New(),
            chord.Voicing.MinimumMidiNote,
            chord.Voicing.MaximumMidiNote,
            chord.Voicing.Voices.Select((voice, position) => new ChordVoice(
                ChordVoiceId.New(), position, voice.Pitch, HarmonyProvenance.Manual)).ToList());
        return new HarmonyChord(HarmonyChordId.New(), chord.Chord, chord.Start, chord.DurationBars, HarmonyProvenance.Manual, voicing);
    }
}

public sealed class MoveSectionCommand(SectionId sectionId, int targetIndex) : IProjectCommand
{
    private int? _previousIndex;

    public void Execute(SongProject project)
    {
        _previousIndex ??= project.IndexOf(sectionId);
        project.MoveSection(sectionId, targetIndex);
    }

    public void Undo(SongProject project)
    {
        if (_previousIndex is null) throw new InvalidOperationException("Command has not been executed.");
        project.MoveSection(sectionId, _previousIndex.Value);
    }
}

public sealed class SetSectionDurationCommand(SectionId sectionId, int durationBars) : IProjectCommand
{
    private int? _previousDurationBars;

    public void Execute(SongProject project)
    {
        _previousDurationBars ??= project.Timeline.FindSection(sectionId).DurationBars;
        project.SetSectionDuration(sectionId, durationBars);
    }

    public void Undo(SongProject project)
    {
        if (_previousDurationBars is null) throw new InvalidOperationException("Command has not been executed.");
        project.SetSectionDuration(sectionId, _previousDurationBars.Value);
    }
}

public sealed class RemoveSectionCommand(SectionId sectionId) : IProjectCommand
{
    private SongSection? _removedSection;
    private int? _removedIndex;
    private int? _removedDurationBars;
    private SectionArrangement? _removedArrangement;
    private IReadOnlyList<SectionRoleAssignment>? _removedRoles;
    private IReadOnlyList<MusicalPart>? _removedMusicalParts;

    public void Execute(SongProject project)
    {
        _removedArrangement ??= project.FindSectionArrangement(sectionId);
        _removedRoles ??= project.ArrangementRoles.Where(item => item.SectionId == sectionId).ToList();
        _removedMusicalParts ??= project.MusicalParts.Where(item => item.SectionId == sectionId).ToList();
        var removed = project.RemoveSection(sectionId);
        _removedSection ??= removed.Section;
        _removedIndex ??= removed.Index;
        _removedDurationBars ??= removed.DurationBars;
    }

    public void Undo(SongProject project)
    {
        if (_removedSection is null || _removedIndex is null || _removedDurationBars is null)
            throw new InvalidOperationException("Command has not been executed.");
        project.InsertSection(_removedIndex.Value, _removedSection, _removedDurationBars.Value);
        if (_removedArrangement is not null) project.RestoreSectionArrangement(sectionId, _removedArrangement);
        if (_removedRoles is { Count: > 0 }) project.RestoreSectionRoles(sectionId, _removedRoles);
        if (_removedMusicalParts is { Count: > 0 }) project.RestoreMusicalParts(sectionId, _removedMusicalParts);
    }
}

public sealed class AddMusicalPartCommand(
    SectionId sectionId,
    ArrangementRole role,
    string label,
    IReadOnlyList<NoteEventId> noteEventIds,
    string? instrumentProfileId = null) : IProjectCommand
{
    private MusicalPart? _created;

    public void Execute(SongProject project)
    {
        if (_created is null)
        {
            var assigned = MusicalPartInstrumentAssignment.RequireCatalogId(instrumentProfileId);
            _created = project.AddMusicalPart(sectionId, role, label, noteEventIds, assigned);
        }
        else project.RestoreMusicalPart(_created);
    }

    public void Undo(SongProject project)
    {
        if (_created is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveMusicalPart(_created.Id);
    }
}

public sealed class RemoveMusicalPartCommand(MusicalPartId musicalPartId) : IProjectCommand
{
    private MusicalPart? _removed;

    public void Execute(SongProject project)
    {
        _removed = project.RemoveMusicalPart(musicalPartId);
    }

    public void Undo(SongProject project)
    {
        if (_removed is null) throw new InvalidOperationException("Command has not been executed.");
        project.RestoreMusicalPart(_removed);
    }
}

public sealed class SetMusicalPartCommand(
    MusicalPartId musicalPartId,
    string label,
    IReadOnlyList<NoteEventId> noteEventIds,
    string? instrumentProfileId = null) : IProjectCommand
{
    private MusicalPart? _before;
    private MusicalPart? _after;

    public void Execute(SongProject project)
    {
        if (_after is null)
        {
            _before = project.MusicalParts.SingleOrDefault(item => item.Id == musicalPartId)
                ?? throw new KeyNotFoundException($"Musical part '{musicalPartId}' was not found.");
            var assigned = MusicalPartInstrumentAssignment.RequireCatalogId(instrumentProfileId);
            _after = project.SetMusicalPart(musicalPartId, label, noteEventIds, assigned);
        }
        else project.RestoreMusicalPart(_after);
    }

    public void Undo(SongProject project)
    {
        if (_before is null) throw new InvalidOperationException("Command has not been executed.");
        project.RestoreMusicalPart(_before);
    }
}

public sealed class UseLowEndSupportProposalCommand(SectionId sectionId) : IProjectCommand
{
    private IReadOnlyList<NoteEvent>? _createdNotes;
    private MusicalPart? _createdPart;

    public void Execute(SongProject project)
    {
        if (_createdPart is null)
        {
            var proposal = LowEndSupportRealizer.Propose(project, sectionId);
            var createdNotes = new List<NoteEvent>();
            var partNoteIds = new List<NoteEventId>();
            foreach (var item in proposal.Events)
            {
                if (item.ExistingNoteEventId is { } existingId) partNoteIds.Add(existingId);
                else
                {
                    var created = project.AddNoteEvent(item.Pitch, item.StartTick, item.DurationTicks, item.Velocity);
                    createdNotes.Add(created);
                    partNoteIds.Add(created.Id);
                }
            }
            _createdNotes = createdNotes;
            _createdPart = project.AddMusicalPart(sectionId, ArrangementRole.LowEndSupport, proposal.PartLabel, partNoteIds);
            return;
        }

        foreach (var note in _createdNotes ?? []) project.RestoreNoteEvent(note);
        project.RestoreMusicalPart(_createdPart);
    }

    public void Undo(SongProject project)
    {
        if (_createdPart is null || _createdNotes is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveMusicalPart(_createdPart.Id);
        foreach (var note in _createdNotes) project.RemoveNoteEvent(note.Id);
    }
}

public sealed class UsePulseProposalCommand(SectionId sectionId) : IProjectCommand
{
    private IReadOnlyList<NoteEvent>? _createdNotes;
    private MusicalPart? _createdPart;

    public void Execute(SongProject project)
    {
        if (_createdPart is null)
        {
            var proposal = PulseRealizer.Propose(project, sectionId);
            var createdNotes = new List<NoteEvent>();
            var partNoteIds = new List<NoteEventId>();
            foreach (var item in proposal.Events)
            {
                if (item.ExistingNoteEventId is { } existingId) partNoteIds.Add(existingId);
                else
                {
                    var created = project.AddNoteEvent(item.Pitch, item.StartTick, item.DurationTicks, item.Velocity);
                    createdNotes.Add(created);
                    partNoteIds.Add(created.Id);
                }
            }
            _createdNotes = createdNotes;
            _createdPart = project.AddMusicalPart(sectionId, ArrangementRole.Pulse, proposal.PartLabel, partNoteIds);
            return;
        }

        foreach (var note in _createdNotes ?? []) project.RestoreNoteEvent(note);
        project.RestoreMusicalPart(_createdPart);
    }

    public void Undo(SongProject project)
    {
        if (_createdPart is null || _createdNotes is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveMusicalPart(_createdPart.Id);
        foreach (var note in _createdNotes) project.RemoveNoteEvent(note.Id);
    }
}

public sealed class UseHarmonySupportProposalCommand(SectionId sectionId) : IProjectCommand
{
    private IReadOnlyList<NoteEvent>? _createdNotes;
    private MusicalPart? _createdPart;

    public void Execute(SongProject project)
    {
        if (_createdPart is null)
        {
            var proposal = HarmonySupportRealizer.Propose(project, sectionId);
            var createdNotes = new List<NoteEvent>();
            var partNoteIds = new List<NoteEventId>();
            foreach (var item in proposal.Events)
            {
                if (item.ExistingNoteEventId is { } existingId) partNoteIds.Add(existingId);
                else
                {
                    var created = project.AddNoteEvent(item.Pitch, item.StartTick, item.DurationTicks, item.Velocity);
                    createdNotes.Add(created);
                    partNoteIds.Add(created.Id);
                }
            }
            _createdNotes = createdNotes;
            _createdPart = project.AddMusicalPart(sectionId, ArrangementRole.Harmony, proposal.PartLabel, partNoteIds);
            return;
        }

        foreach (var note in _createdNotes ?? []) project.RestoreNoteEvent(note);
        project.RestoreMusicalPart(_createdPart);
    }

    public void Undo(SongProject project)
    {
        if (_createdPart is null || _createdNotes is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveMusicalPart(_createdPart.Id);
        foreach (var note in _createdNotes) project.RemoveNoteEvent(note.Id);
    }
}

public sealed class UseTextureProposalCommand(SectionId sectionId) : IProjectCommand
{
    private IReadOnlyList<NoteEvent>? _createdNotes;
    private MusicalPart? _createdPart;

    public void Execute(SongProject project)
    {
        if (_createdPart is null)
        {
            var proposal = TextureRealizer.Propose(project, sectionId);
            var createdNotes = new List<NoteEvent>();
            var partNoteIds = new List<NoteEventId>();
            foreach (var item in proposal.Events)
            {
                if (item.ExistingNoteEventId is { } existingId) partNoteIds.Add(existingId);
                else
                {
                    var created = project.AddNoteEvent(item.Pitch, item.StartTick, item.DurationTicks, item.Velocity);
                    createdNotes.Add(created);
                    partNoteIds.Add(created.Id);
                }
            }
            _createdNotes = createdNotes;
            _createdPart = project.AddMusicalPart(sectionId, ArrangementRole.Texture, proposal.PartLabel, partNoteIds);
            return;
        }

        foreach (var note in _createdNotes ?? []) project.RestoreNoteEvent(note);
        project.RestoreMusicalPart(_createdPart);
    }

    public void Undo(SongProject project)
    {
        if (_createdPart is null || _createdNotes is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveMusicalPart(_createdPart.Id);
        foreach (var note in _createdNotes) project.RemoveNoteEvent(note.Id);
    }
}

public sealed class UseHookReinforcementProposalCommand(SectionId sectionId) : IProjectCommand
{
    private IReadOnlyList<NoteEvent>? _createdNotes;
    private MusicalPart? _createdPart;

    public void Execute(SongProject project)
    {
        if (_createdPart is null)
        {
            var proposal = HookReinforcementRealizer.Propose(project, sectionId);
            var createdNotes = new List<NoteEvent>();
            var partNoteIds = new List<NoteEventId>();
            foreach (var item in proposal.Events)
            {
                if (item.ExistingNoteEventId is { } existingId) partNoteIds.Add(existingId);
                else
                {
                    var created = project.AddNoteEvent(item.Pitch, item.StartTick, item.DurationTicks, item.Velocity);
                    createdNotes.Add(created);
                    partNoteIds.Add(created.Id);
                }
            }
            _createdNotes = createdNotes;
            _createdPart = project.AddMusicalPart(sectionId, ArrangementRole.HookReinforcement, proposal.PartLabel, partNoteIds);
            return;
        }

        foreach (var note in _createdNotes ?? []) project.RestoreNoteEvent(note);
        project.RestoreMusicalPart(_createdPart);
    }

    public void Undo(SongProject project)
    {
        if (_createdPart is null || _createdNotes is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveMusicalPart(_createdPart.Id);
        foreach (var note in _createdNotes) project.RemoveNoteEvent(note.Id);
    }
}

public sealed class UseCountermelodyProposalCommand(SectionId sectionId) : IProjectCommand
{
    private IReadOnlyList<NoteEvent>? _createdNotes;
    private MusicalPart? _createdPart;

    public void Execute(SongProject project)
    {
        if (_createdPart is null)
        {
            var proposal = CountermelodyRealizer.Propose(project, sectionId);
            var createdNotes = new List<NoteEvent>();
            var partNoteIds = new List<NoteEventId>();
            foreach (var item in proposal.Events)
            {
                if (item.ExistingNoteEventId is { } existingId) partNoteIds.Add(existingId);
                else
                {
                    var created = project.AddNoteEvent(item.Pitch, item.StartTick, item.DurationTicks, item.Velocity);
                    createdNotes.Add(created);
                    partNoteIds.Add(created.Id);
                }
            }
            _createdNotes = createdNotes;
            _createdPart = project.AddMusicalPart(sectionId, ArrangementRole.Countermelody, proposal.PartLabel, partNoteIds);
            return;
        }

        foreach (var note in _createdNotes ?? []) project.RestoreNoteEvent(note);
        project.RestoreMusicalPart(_createdPart);
    }

    public void Undo(SongProject project)
    {
        if (_createdPart is null || _createdNotes is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveMusicalPart(_createdPart.Id);
        foreach (var note in _createdNotes) project.RemoveNoteEvent(note.Id);
    }
}

public sealed class UseAccentProposalCommand(SectionId sectionId) : IProjectCommand
{
    private IReadOnlyList<NoteEvent>? _createdNotes;
    private MusicalPart? _createdPart;

    public void Execute(SongProject project)
    {
        if (_createdPart is null)
        {
            var proposal = AccentRealizer.Propose(project, sectionId);
            var createdNotes = new List<NoteEvent>();
            var partNoteIds = new List<NoteEventId>();
            foreach (var item in proposal.Events)
            {
                if (item.ExistingNoteEventId is { } existingId) partNoteIds.Add(existingId);
                else
                {
                    var created = project.AddNoteEvent(item.Pitch, item.StartTick, item.DurationTicks, item.Velocity);
                    createdNotes.Add(created);
                    partNoteIds.Add(created.Id);
                }
            }
            _createdNotes = createdNotes;
            _createdPart = project.AddMusicalPart(sectionId, ArrangementRole.Accent, proposal.PartLabel, partNoteIds);
            return;
        }

        foreach (var note in _createdNotes ?? []) project.RestoreNoteEvent(note);
        project.RestoreMusicalPart(_createdPart);
    }

    public void Undo(SongProject project)
    {
        if (_createdPart is null || _createdNotes is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveMusicalPart(_createdPart.Id);
        foreach (var note in _createdNotes) project.RemoveNoteEvent(note.Id);
    }
}

public sealed class SetSectionRoleCommand(
    SectionId sectionId,
    ArrangementRole role,
    bool present) : IProjectCommand
{
    private SectionRoleAssignment? _before;
    private SectionRoleAssignment? _after;
    private bool _captured;

    public void Execute(SongProject project)
    {
        if (!_captured)
        {
            _before = project.FindSectionRole(sectionId, role);
            _captured = true;
            _after = present
                ? _before ?? project.SetSectionRole(sectionId, role)
                : null;
            if (!present && _before is not null) project.RemoveSectionRole(sectionId, role);
            return;
        }

        if (_after is null)
        {
            if (project.FindSectionRole(sectionId, role) is not null) project.RemoveSectionRole(sectionId, role);
        }
        else project.RestoreSectionRole(_after);
    }

    public void Undo(SongProject project)
    {
        if (!_captured) throw new InvalidOperationException("Command has not been executed.");
        if (_before is null)
        {
            if (project.FindSectionRole(sectionId, role) is not null) project.RemoveSectionRole(sectionId, role);
        }
        else project.RestoreSectionRole(_before);
    }
}

public sealed class SetSectionArrangementCommand(
    SectionId sectionId,
    SectionEnergy energy,
    SectionDensity density) : IProjectCommand
{
    private SectionArrangement? _previous;
    private SectionArrangement? _next;
    private bool _captured;

    public void Execute(SongProject project)
    {
        if (!_captured) { _previous = project.FindSectionArrangement(sectionId); _captured = true; }
        if (_next is null) _next = project.SetSectionArrangement(sectionId, energy, density);
        else project.RestoreSectionArrangement(sectionId, _next);
    }

    public void Undo(SongProject project)
    {
        if (!_captured) throw new InvalidOperationException("Command has not been executed.");
        project.RestoreSectionArrangement(sectionId, _previous);
    }
}

public sealed class AddNoteEventCommand(
    RegisteredPitch pitch,
    long startTick,
    long durationTicks,
    int velocity) : IProjectCommand
{
    private NoteEvent? _created;

    public NoteEventId? NoteEventId => _created?.Id;

    public void Execute(SongProject project)
    {
        if (_created is null) _created = project.AddNoteEvent(pitch, startTick, durationTicks, velocity);
        else project.RestoreNoteEvent(_created);
    }

    public void Undo(SongProject project)
    {
        if (_created is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveNoteEvent(_created.Id);
    }
}

public sealed class SetNoteEventCommand(
    NoteEventId noteEventId,
    RegisteredPitch pitch,
    long startTick,
    long durationTicks,
    int velocity) : IProjectCommand
{
    private NoteEvent? _before;
    private NoteEvent? _after;

    public void Execute(SongProject project)
    {
        if (_after is null)
        {
            _before = project.NoteEvents.SingleOrDefault(item => item.Id == noteEventId)
                ?? throw new KeyNotFoundException($"Note event '{noteEventId}' was not found.");
            _after = project.SetNoteEvent(noteEventId, pitch, startTick, durationTicks, velocity);
        }
        else project.RestoreNoteEvent(_after);
    }

    public void Undo(SongProject project)
    {
        if (_before is null) throw new InvalidOperationException("Command has not been executed.");
        project.RestoreNoteEvent(_before);
    }
}

public sealed class RemoveNoteEventCommand(NoteEventId noteEventId) : IProjectCommand
{
    private NoteEvent? _removed;

    public void Execute(SongProject project)
    {
        if (_removed is null) _removed = project.RemoveNoteEvent(noteEventId);
        else project.RemoveNoteEvent(noteEventId);
    }

    public void Undo(SongProject project)
    {
        if (_removed is null) throw new InvalidOperationException("Command has not been executed.");
        project.RestoreNoteEvent(_removed);
    }
}

public sealed class UseHarmonyNoteSketchCommand(SectionId sectionId) : IProjectCommand
{
    private IReadOnlyList<NoteEvent>? _created;

    public void Execute(SongProject project)
    {
        if (_created is null)
        {
            var sketch = HarmonyNoteSketcher.Project(project, sectionId);
            _created = sketch.Events.Select(item => project.AddNoteEvent(
                item.Pitch,
                item.StartTick,
                item.DurationTicks,
                item.Velocity)).ToList();
            return;
        }

        foreach (var noteEvent in _created) project.RestoreNoteEvent(noteEvent);
    }

    public void Undo(SongProject project)
    {
        if (_created is null) throw new InvalidOperationException("Command has not been executed.");
        foreach (var noteEvent in _created) project.RemoveNoteEvent(noteEvent.Id);
    }
}

public sealed class UsePitchGestureNoteSketchCommand(ProjectAssetId assetId) : IProjectCommand
{
    private IReadOnlyList<NoteEvent>? _created;

    public void Execute(SongProject project)
    {
        if (_created is null)
        {
            var sketch = PitchGestureNoteSketcher.Project(project, assetId);
            _created = sketch.Events.Select(item => project.AddNoteEvent(
                item.Pitch,
                item.StartTick,
                item.DurationTicks,
                item.Velocity)).ToList();
            return;
        }

        foreach (var noteEvent in _created) project.RestoreNoteEvent(noteEvent);
    }

    public void Undo(SongProject project)
    {
        if (_created is null) throw new InvalidOperationException("Command has not been executed.");
        foreach (var noteEvent in _created) project.RemoveNoteEvent(noteEvent.Id);
    }
}

public sealed class UseOnsetGestureNoteSketchCommand(ProjectAssetId assetId) : IProjectCommand
{
    private IReadOnlyList<NoteEvent>? _created;

    public void Execute(SongProject project)
    {
        if (_created is null)
        {
            var sketch = OnsetGestureNoteSketcher.Project(project, assetId);
            _created = sketch.Events.Select(item => project.AddNoteEvent(
                item.Pitch,
                item.StartTick,
                item.DurationTicks,
                item.Velocity)).ToList();
            return;
        }

        foreach (var noteEvent in _created) project.RestoreNoteEvent(noteEvent);
    }

    public void Undo(SongProject project)
    {
        if (_created is null) throw new InvalidOperationException("Command has not been executed.");
        foreach (var noteEvent in _created) project.RemoveNoteEvent(noteEvent.Id);
    }
}

public sealed class UseLoudnessGestureNoteSketchCommand(ProjectAssetId assetId) : IProjectCommand
{
    private IReadOnlyList<NoteEvent>? _created;

    public void Execute(SongProject project)
    {
        if (_created is null)
        {
            var sketch = LoudnessGestureNoteSketcher.Project(project, assetId);
            _created = sketch.Events.Select(item => project.AddNoteEvent(
                item.Pitch,
                item.StartTick,
                item.DurationTicks,
                item.Velocity)).ToList();
            return;
        }

        foreach (var noteEvent in _created) project.RestoreNoteEvent(noteEvent);
    }

    public void Undo(SongProject project)
    {
        if (_created is null) throw new InvalidOperationException("Command has not been executed.");
        foreach (var noteEvent in _created) project.RemoveNoteEvent(noteEvent.Id);
    }
}

public sealed class UseLoudnessGestureExpressionSketchCommand(ProjectAssetId assetId) : IProjectCommand
{
    private ExpressionCurve? _created;

    public void Execute(SongProject project)
    {
        if (_created is null)
        {
            var sketch = LoudnessGestureExpressionSketcher.Project(project, assetId);
            _created = project.AddExpressionCurve(sketch.Name, sketch.Kind, sketch.Points);
            return;
        }

        project.RestoreExpressionCurve(_created);
    }

    public void Undo(SongProject project)
    {
        if (_created is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveExpressionCurve(_created.Id);
    }
}

public sealed class RemoveExpressionCurveCommand(ExpressionCurveId expressionCurveId) : IProjectCommand
{
    private ExpressionCurve? _removed;

    public void Execute(SongProject project)
    {
        if (_removed is null) _removed = project.RemoveExpressionCurve(expressionCurveId);
        else project.RemoveExpressionCurve(expressionCurveId);
    }

    public void Undo(SongProject project)
    {
        if (_removed is null) throw new InvalidOperationException("Command has not been executed.");
        project.RestoreExpressionCurve(_removed);
    }
}

public sealed class SetVocalTakePlacementCommand(ProjectAssetId assetId, MusicalPosition start) : IProjectCommand
{
    private VocalTakePlacement? _previous;
    private VocalTakePlacement? _applied;

    public void Execute(SongProject project)
    {
        if (_applied is null)
        {
            _previous = project.FindVocalTakePlacement(assetId);
            _applied = project.SetVocalTakePlacement(assetId, start);
            return;
        }

        project.RestoreVocalTakePlacement(_applied);
    }

    public void Undo(SongProject project)
    {
        if (_applied is null) throw new InvalidOperationException("Command has not been executed.");
        if (_previous is null) project.ClearVocalTakePlacement(assetId);
        else project.RestoreVocalTakePlacement(_previous);
    }
}

public sealed class ClearVocalTakePlacementCommand(ProjectAssetId assetId) : IProjectCommand
{
    private VocalTakePlacement? _previous;

    public void Execute(SongProject project)
    {
        _previous ??= project.FindVocalTakePlacement(assetId)
            ?? throw new KeyNotFoundException($"Vocal take '{assetId}' has no song placement.");
        if (project.FindVocalTakePlacement(assetId) is not null)
            project.ClearVocalTakePlacement(assetId);
    }

    public void Undo(SongProject project)
    {
        if (_previous is null) throw new InvalidOperationException("Command has not been executed.");
        project.RestoreVocalTakePlacement(_previous);
    }
}

public sealed class SplitLyricPhraseCommand(
    SectionId sectionId,
    LyricLineId lineId,
    LyricWordId wordId) : IProjectCommand
{
    private LyricStructureSnapshot? _before;
    private LyricStructureSnapshot? _after;

    public void Execute(SongProject project)
    {
        project.EnsureLyricLineUnlocked(lineId);
        var line = FindLine(project);
        if (_after is null)
        {
            _before = LyricStructureSnapshots.Create(line);
            line.SplitPhraseAfter(wordId);
            project.ReconcileLocks();
            _after = LyricStructureSnapshots.Create(line);
        }
        else
        {
            LyricStructureSnapshots.Restore(line, _after);
            project.ReconcileLocks();
        }
        project.Touch();
    }

    public void Undo(SongProject project)
    {
        if (_before is null) throw new InvalidOperationException("Command has not been executed.");
        LyricStructureSnapshots.Restore(FindLine(project), _before);
        project.ReconcileLocks();
        project.Touch();
    }

    private LyricLine FindLine(SongProject project) => project.FindSection(sectionId).FindLyricLine(lineId);
}

internal sealed record LyricStructureSnapshot(
    IReadOnlyList<LyricPhrase> Phrases,
    IReadOnlyList<RhythmCandidate> RhythmCandidates);

internal static class LyricStructureSnapshots
{
    public static LyricStructureSnapshot Create(LyricLine line) => new(
        PhraseSnapshots.Create(line.Phrases),
        RhythmCandidateSnapshots.Create(line.RhythmCandidates));

    public static void Restore(LyricLine line, LyricStructureSnapshot snapshot)
    {
        line.RestorePhrases(snapshot.Phrases);
        line.RestoreRhythmCandidates(snapshot.RhythmCandidates);
    }
}

internal static class PhraseSnapshots
{
    public static IReadOnlyList<LyricPhrase> Create(IEnumerable<LyricPhrase> phrases) =>
        phrases.Select(ClonePhrase).ToList();

    private static LyricPhrase ClonePhrase(LyricPhrase phrase) => new(
        phrase.Id,
        phrase.Position,
        phrase.WordIds.ToList(),
        phrase.Source,
        phrase.Prosody is null ? null : new ProsodicPattern(
            phrase.Prosody.Id,
            phrase.Prosody.Units.Select(unit => new ProsodicUnit(
                unit.Id,
                unit.SyllableId,
                unit.Position,
                unit.Weight,
                unit.Provenance)).ToList()));
}

public sealed class JoinLyricPhraseCommand(
    SectionId sectionId,
    LyricLineId lineId,
    LyricPhraseId phraseId) : IProjectCommand
{
    private LyricStructureSnapshot? _before;
    private LyricStructureSnapshot? _after;

    public void Execute(SongProject project)
    {
        project.EnsureLyricLineUnlocked(lineId);
        var line = FindLine(project);
        if (_after is null)
        {
            _before = LyricStructureSnapshots.Create(line);
            line.JoinPhraseWithPrevious(phraseId);
            project.ReconcileLocks();
            _after = LyricStructureSnapshots.Create(line);
        }
        else
        {
            LyricStructureSnapshots.Restore(line, _after);
            project.ReconcileLocks();
        }
        project.Touch();
    }

    public void Undo(SongProject project)
    {
        if (_before is null) throw new InvalidOperationException("Command has not been executed.");
        LyricStructureSnapshots.Restore(FindLine(project), _before);
        project.ReconcileLocks();
        project.Touch();
    }

    private LyricLine FindLine(SongProject project) => project.FindSection(sectionId).FindLyricLine(lineId);
}

public sealed class SetSyllableStressCommand(
    SectionId sectionId,
    LyricLineId lineId,
    LyricWordId wordId,
    SyllableId syllableId,
    StressLevel? level) : IProjectCommand
{
    private StressMark? _previous;
    private bool _captured;

    public void Execute(SongProject project)
    {
        project.EnsureLyricLineUnlocked(lineId);
        var line = FindLine(project);
        if (!_captured)
        {
            var syllable = FindSyllable(line);
            _previous = syllable.Stress is null
                ? null
                : new StressMark(syllable.Stress.Level, syllable.Stress.Provenance);
            _captured = true;
        }
        line.SetStress(wordId, syllableId, level, StressProvenance.Manual);
        project.Touch();
    }

    public void Undo(SongProject project)
    {
        if (!_captured) throw new InvalidOperationException("Command has not been executed.");
        FindLine(project).SetStress(
            wordId,
            syllableId,
            _previous?.Level,
            _previous?.Provenance ?? StressProvenance.Manual);
        project.Touch();
    }

    private LyricLine FindLine(SongProject project) => project.FindSection(sectionId).FindLyricLine(lineId);
    private LyricSyllable FindSyllable(LyricLine line) =>
        line.Words.SingleOrDefault(item => item.Id == wordId)?.Syllables.SingleOrDefault(item => item.Id == syllableId)
        ?? throw new KeyNotFoundException($"Syllable '{syllableId}' was not found in lyric word '{wordId}'.");
}

public sealed class SetProsodicWeightCommand(
    SectionId sectionId,
    LyricLineId lineId,
    LyricPhraseId phraseId,
    SyllableId syllableId,
    ProsodicWeight? weight) : IProjectCommand
{
    private IReadOnlyList<LyricPhrase>? _before;
    private IReadOnlyList<LyricPhrase>? _after;

    public void Execute(SongProject project)
    {
        project.EnsureLyricLineUnlocked(lineId);
        var line = FindLine(project);
        if (_after is null)
        {
            _before = PhraseSnapshots.Create(line.Phrases);
            line.SetProsodicWeight(phraseId, syllableId, weight, ProsodyProvenance.Manual);
            _after = PhraseSnapshots.Create(line.Phrases);
        }
        else
        {
            line.RestorePhrases(_after);
        }
        project.Touch();
    }

    public void Undo(SongProject project)
    {
        if (_before is null) throw new InvalidOperationException("Command has not been executed.");
        FindLine(project).RestorePhrases(_before);
        project.Touch();
    }

    private LyricLine FindLine(SongProject project) => project.FindSection(sectionId).FindLyricLine(lineId);
}

public sealed class SetSyllablePlacementCommand(
    SectionId sectionId,
    LyricLineId lineId,
    SyllableId syllableId,
    BeatPosition? position) : IProjectCommand
{
    private IReadOnlyList<SyllablePlacement>? _before;
    private IReadOnlyList<SyllablePlacement>? _after;

    public void Execute(SongProject project)
    {
        var line = FindLine(project);
        if (_after is null)
        {
            _before = PlacementSnapshots.Create(line.SyllablePlacements);
            project.SetSyllablePlacement(
                sectionId,
                lineId,
                syllableId,
                position,
                PlacementProvenance.Manual);
            _after = PlacementSnapshots.Create(line.SyllablePlacements);
        }
        else
        {
            line.RestoreSyllablePlacements(_after);
            project.Touch();
        }
    }

    public void Undo(SongProject project)
    {
        if (_before is null) throw new InvalidOperationException("Command has not been executed.");
        FindLine(project).RestoreSyllablePlacements(_before);
        project.Touch();
    }

    private LyricLine FindLine(SongProject project) => project.FindSection(sectionId).FindLyricLine(lineId);
}

internal static class PlacementSnapshots
{
    public static IReadOnlyList<SyllablePlacement> Create(IEnumerable<SyllablePlacement> placements) =>
        placements.Select(item => new SyllablePlacement(
            item.Id,
            item.SyllableId,
            item.Position,
            item.Provenance)).ToList();
}

public sealed class CaptureRhythmCandidateCommand(
    SectionId sectionId,
    LyricLineId lineId,
    LyricPhraseId phraseId,
    string label) : IProjectCommand
{
    private RhythmCandidate? _candidate;
    private int? _index;

    public RhythmCandidateId? CandidateId => _candidate?.Id;

    public void Execute(SongProject project)
    {
        var line = FindLine(project);
        if (_candidate is null)
        {
            _candidate = RhythmCandidateSnapshots.Clone(project.CaptureRhythmCandidate(
                sectionId,
                lineId,
                phraseId,
                label,
                RhythmCandidateProvenance.Manual));
            _index = line.RhythmCandidates.Count - 1;
        }
        else
        {
            line.InsertRhythmCandidate(_index!.Value, _candidate);
            project.Touch();
        }
    }

    public void Undo(SongProject project)
    {
        if (_candidate is null) throw new InvalidOperationException("Command has not been executed.");
        FindLine(project).RemoveRhythmCandidate(_candidate.Id);
        project.Touch();
    }

    private LyricLine FindLine(SongProject project) => project.FindSection(sectionId).FindLyricLine(lineId);
}

public sealed class RenameRhythmCandidateCommand(
    SectionId sectionId,
    LyricLineId lineId,
    RhythmCandidateId candidateId,
    string label) : IProjectCommand
{
    private string? _previousLabel;

    public void Execute(SongProject project)
    {
        var line = FindLine(project);
        _previousLabel ??= line.RhythmCandidates.SingleOrDefault(item => item.Id == candidateId)?.Label
            ?? throw new KeyNotFoundException($"Rhythm candidate '{candidateId}' was not found.");
        line.RenameRhythmCandidate(candidateId, label);
        project.Touch();
    }

    public void Undo(SongProject project)
    {
        if (_previousLabel is null) throw new InvalidOperationException("Command has not been executed.");
        FindLine(project).RenameRhythmCandidate(candidateId, _previousLabel);
        project.Touch();
    }

    private LyricLine FindLine(SongProject project) => project.FindSection(sectionId).FindLyricLine(lineId);
}

public sealed class RemoveRhythmCandidateCommand(
    SectionId sectionId,
    LyricLineId lineId,
    RhythmCandidateId candidateId) : IProjectCommand
{
    private RhythmCandidate? _removed;
    private int? _index;

    public void Execute(SongProject project)
    {
        var removed = FindLine(project).RemoveRhythmCandidate(candidateId);
        _removed ??= RhythmCandidateSnapshots.Clone(removed.Candidate);
        _index ??= removed.Index;
        project.Touch();
    }

    public void Undo(SongProject project)
    {
        if (_removed is null || _index is null) throw new InvalidOperationException("Command has not been executed.");
        FindLine(project).InsertRhythmCandidate(_index.Value, _removed);
        project.Touch();
    }

    private LyricLine FindLine(SongProject project) => project.FindSection(sectionId).FindLyricLine(lineId);
}

public sealed class ApplyRhythmCandidateCommand(
    SectionId sectionId,
    LyricLineId lineId,
    RhythmCandidateId candidateId) : IProjectCommand
{
    private IReadOnlyList<SyllablePlacement>? _before;
    private IReadOnlyList<SyllablePlacement>? _after;

    public void Execute(SongProject project)
    {
        var line = FindLine(project);
        if (_after is null)
        {
            _before = PlacementSnapshots.Create(line.SyllablePlacements);
            project.ApplyRhythmCandidate(sectionId, lineId, candidateId);
            _after = PlacementSnapshots.Create(line.SyllablePlacements);
        }
        else
        {
            line.RestoreSyllablePlacements(_after);
            project.Touch();
        }
    }

    public void Undo(SongProject project)
    {
        if (_before is null) throw new InvalidOperationException("Command has not been executed.");
        FindLine(project).RestoreSyllablePlacements(_before);
        project.Touch();
    }

    private LyricLine FindLine(SongProject project) => project.FindSection(sectionId).FindLyricLine(lineId);
}

public sealed class SetBreathPointCommand(
    SectionId sectionId,
    LyricLineId lineId,
    SyllableId afterSyllableId,
    bool present) : IProjectCommand
{
    private IReadOnlyList<BreathPoint>? _before;
    private IReadOnlyList<BreathPoint>? _after;

    public void Execute(SongProject project)
    {
        project.EnsureLyricLineUnlocked(lineId);
        var line = FindLine(project);
        if (_after is null)
        {
            _before = BreathPointSnapshots.Create(line.BreathPoints);
            line.SetBreathPoint(afterSyllableId, present, BreathProvenance.Manual);
            _after = BreathPointSnapshots.Create(line.BreathPoints);
        }
        else
        {
            line.RestoreBreathPoints(_after);
        }
        project.Touch();
    }

    public void Undo(SongProject project)
    {
        if (_before is null) throw new InvalidOperationException("Command has not been executed.");
        FindLine(project).RestoreBreathPoints(_before);
        project.Touch();
    }

    private LyricLine FindLine(SongProject project) => project.FindSection(sectionId).FindLyricLine(lineId);
}

public sealed class LockLyricLineCommand(LyricLineId lineId) : IProjectCommand
{
    private CreativeLock? _lock;
    private int? _index;

    public CreativeLockId? LockId => _lock?.Id;

    public void Execute(SongProject project)
    {
        if (_lock is null)
        {
            _lock = Clone(project.LockLyricLine(lineId, LockProvenance.Manual));
            _index = project.Locks.Count - 1;
        }
        else
        {
            project.InsertLock(_index!.Value, _lock);
        }
    }

    public void Undo(SongProject project)
    {
        if (_lock is null) throw new InvalidOperationException("Command has not been executed.");
        project.Unlock(_lock.Id);
    }

    private static CreativeLock Clone(CreativeLock lockItem) => new(
        lockItem.Id, lockItem.Scope, lockItem.LineId, lockItem.PhraseId, lockItem.Provenance);
}

public sealed class LockPhraseRhythmCommand(LyricLineId lineId, LyricPhraseId phraseId) : IProjectCommand
{
    private CreativeLock? _lock;
    private int? _index;

    public CreativeLockId? LockId => _lock?.Id;

    public void Execute(SongProject project)
    {
        if (_lock is null)
        {
            _lock = Clone(project.LockPhraseRhythm(lineId, phraseId, LockProvenance.Manual));
            _index = project.Locks.Count - 1;
        }
        else
        {
            project.InsertLock(_index!.Value, _lock);
        }
    }

    public void Undo(SongProject project)
    {
        if (_lock is null) throw new InvalidOperationException("Command has not been executed.");
        project.Unlock(_lock.Id);
    }

    private static CreativeLock Clone(CreativeLock lockItem) => new(
        lockItem.Id, lockItem.Scope, lockItem.LineId, lockItem.PhraseId, lockItem.Provenance);
}

public sealed class UnlockCreativeLockCommand(CreativeLockId lockId) : IProjectCommand
{
    private CreativeLock? _removed;
    private int? _index;

    public void Execute(SongProject project)
    {
        var removed = project.Unlock(lockId);
        _removed ??= new CreativeLock(
            removed.Lock.Id,
            removed.Lock.Scope,
            removed.Lock.LineId,
            removed.Lock.PhraseId,
            removed.Lock.Provenance);
        _index ??= removed.Index;
    }

    public void Undo(SongProject project)
    {
        if (_removed is null || _index is null) throw new InvalidOperationException("Command has not been executed.");
        project.InsertLock(_index.Value, _removed);
    }
}

public sealed class SetKeyCommand(MusicalKey key) : IProjectCommand
{
    private MusicalKey? _previous;

    public void Execute(SongProject project)
    {
        ArgumentNullException.ThrowIfNull(key);
        _previous ??= project.Key;
        project.SetKey(key);
    }

    public void Undo(SongProject project)
    {
        if (_previous is null) throw new InvalidOperationException("Command has not been executed.");
        project.SetKey(_previous);
    }
}

public sealed class AddHarmonyChordCommand(
    SectionId sectionId,
    ChordSymbol chord,
    BeatPosition start,
    int durationBars = 1) : IProjectCommand
{
    private HarmonyChord? _created;

    public HarmonyChordId? HarmonyChordId => _created?.Id;

    public void Execute(SongProject project)
    {
        if (_created is null)
            _created = project.AddHarmonyChord(sectionId, chord, start, durationBars);
        else
            project.ReinsertHarmonyChord(sectionId, _created);
    }

    public void Undo(SongProject project)
    {
        if (_created is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveHarmonyChord(sectionId, _created.Id);
    }
}

public sealed class SetHarmonyChordCommand(
    SectionId sectionId,
    HarmonyChordId harmonyChordId,
    ChordSymbol chord,
    BeatPosition start,
    int durationBars) : IProjectCommand
{
    private HarmonyChord? _previous;

    public void Execute(SongProject project)
    {
        var section = project.FindSection(sectionId);
        _previous ??= section.FindHarmonyChord(harmonyChordId);
        project.SetHarmonyChord(sectionId, harmonyChordId, chord, start, durationBars);
    }

    public void Undo(SongProject project)
    {
        if (_previous is null) throw new InvalidOperationException("Command has not been executed.");
        project.ReinsertHarmonyChord(sectionId, _previous);
    }
}

public sealed class RemoveHarmonyChordCommand(SectionId sectionId, HarmonyChordId harmonyChordId) : IProjectCommand
{
    private HarmonyChord? _removed;

    public void Execute(SongProject project)
    {
        _removed ??= project.RemoveHarmonyChord(sectionId, harmonyChordId);
    }

    public void Undo(SongProject project)
    {
        if (_removed is null) throw new InvalidOperationException("Command has not been executed.");
        project.ReinsertHarmonyChord(sectionId, _removed);
    }
}

public sealed class SetChordVoicingCommand(SectionId sectionId, HarmonyChordId harmonyChordId, IReadOnlyList<RegisteredPitch>? pitches, int minimumMidiNote = 21, int maximumMidiNote = 108) : IProjectCommand
{
    private ChordVoicing? _previous;
    private ChordVoicing? _next;
    private bool _captured;
    public void Execute(SongProject project)
    {
        if (!_captured) { _previous = project.FindSection(sectionId).FindHarmonyChord(harmonyChordId).Voicing; _captured = true; }
        if (_next is null)
            _next = project.SetChordVoicing(sectionId, harmonyChordId, pitches, minimumMidiNote, maximumMidiNote).Voicing;
        else
            project.RestoreChordVoicing(sectionId, harmonyChordId, _next);
    }
    public void Undo(SongProject project)
    {
        if (!_captured) throw new InvalidOperationException("Command has not been executed.");
        project.RestoreChordVoicing(sectionId, harmonyChordId, _previous);
    }
}

public sealed class CaptureHarmonyCandidateCommand(SectionId sectionId, string label) : IProjectCommand
{
    private HarmonyCandidate? _candidate;
    private int? _index;

    public HarmonyCandidateId? CandidateId => _candidate?.Id;

    public void Execute(SongProject project)
    {
        if (_candidate is null)
        {
            _candidate = HarmonyCandidateSnapshots.Clone(project.CaptureHarmonyCandidate(sectionId, label));
            _index = project.FindSection(sectionId).HarmonyCandidates.Count - 1;
        }
        else project.ReinsertHarmonyCandidate(sectionId, _index!.Value, _candidate);
    }

    public void Undo(SongProject project)
    {
        if (_candidate is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveHarmonyCandidate(sectionId, _candidate.Id);
    }
}

public sealed class RenameHarmonyCandidateCommand(
    SectionId sectionId,
    HarmonyCandidateId candidateId,
    string label) : IProjectCommand
{
    private string? _previousLabel;

    public void Execute(SongProject project)
    {
        _previousLabel ??= project.FindSection(sectionId).FindHarmonyCandidate(candidateId).Label;
        project.RenameHarmonyCandidate(sectionId, candidateId, label);
    }

    public void Undo(SongProject project)
    {
        if (_previousLabel is null) throw new InvalidOperationException("Command has not been executed.");
        project.RenameHarmonyCandidate(sectionId, candidateId, _previousLabel);
    }
}

public sealed class RemoveHarmonyCandidateCommand(SectionId sectionId, HarmonyCandidateId candidateId) : IProjectCommand
{
    private HarmonyCandidate? _removed;
    private int? _index;

    public void Execute(SongProject project)
    {
        var removed = project.RemoveHarmonyCandidate(sectionId, candidateId);
        _removed ??= HarmonyCandidateSnapshots.Clone(removed.Candidate);
        _index ??= removed.Index;
    }

    public void Undo(SongProject project)
    {
        if (_removed is null || _index is null) throw new InvalidOperationException("Command has not been executed.");
        project.ReinsertHarmonyCandidate(sectionId, _index.Value, _removed);
    }
}

public sealed class ApplyHarmonyCandidateCommand(SectionId sectionId, HarmonyCandidateId candidateId) : IProjectCommand
{
    private IReadOnlyList<HarmonyChord>? _previous;

    public void Execute(SongProject project)
    {
        _previous ??= project.FindSection(sectionId).Harmony.Select(HarmonyChordSnapshots.Clone).ToList();
        project.ApplyHarmonyCandidate(sectionId, candidateId);
    }

    public void Undo(SongProject project)
    {
        if (_previous is null) throw new InvalidOperationException("Command has not been executed.");
        project.ReplaceSectionHarmony(sectionId, _previous.Select(HarmonyChordSnapshots.Clone));
    }
}

internal static class HarmonyChordSnapshots
{
    public static HarmonyChord Clone(HarmonyChord chord) =>
        new(chord.Id, chord.Chord, chord.Start, chord.DurationBars, chord.Provenance, chord.Voicing);
}

internal static class HarmonyCandidateSnapshots
{
    public static HarmonyCandidate Clone(HarmonyCandidate candidate) => new(
        candidate.Id,
        candidate.Label,
        candidate.Provenance,
        candidate.Events.Select(item => new HarmonyCandidateEvent(
            item.Id, item.Position, item.Chord, item.Start, item.DurationBars)).ToList());
}

internal static class RhythmCandidateSnapshots
{
    public static IReadOnlyList<RhythmCandidate> Create(IEnumerable<RhythmCandidate> candidates) =>
        candidates.Select(Clone).ToList();

    public static RhythmCandidate Clone(RhythmCandidate candidate) => new(
        candidate.Id,
        candidate.PhraseId,
        candidate.Label,
        candidate.Provenance,
        candidate.Events.Select(item => new RhythmCandidateEvent(
            item.Id,
            item.SyllableId,
            item.Position,
            item.BeatPosition)).ToList());
}

internal static class BreathPointSnapshots
{
    public static IReadOnlyList<BreathPoint> Create(IEnumerable<BreathPoint> breathPoints) =>
        breathPoints.Select(item => new BreathPoint(item.Id, item.AfterSyllableId, item.Provenance)).ToList();
}
