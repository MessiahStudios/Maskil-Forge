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
    private IReadOnlyList<LyricPhrase>? _before;
    private IReadOnlyList<LyricPhrase>? _after;

    public void Execute(SongProject project)
    {
        var line = FindLine(project);
        if (_after is null)
        {
            _before = PhraseSnapshots.Create(line.Phrases);
            line.SplitPhraseAfter(wordId);
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
    private IReadOnlyList<LyricPhrase>? _before;
    private IReadOnlyList<LyricPhrase>? _after;

    public void Execute(SongProject project)
    {
        var line = FindLine(project);
        if (_after is null)
        {
            _before = PhraseSnapshots.Create(line.Phrases);
            line.JoinPhraseWithPrevious(phraseId);
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
