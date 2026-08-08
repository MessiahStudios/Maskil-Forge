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

    public void Execute(SongProject project)
    {
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
