using System.Collections.Concurrent;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Api;

public sealed class ProjectWorkspace(IProjectRepository repository)
{
    private readonly ConcurrentDictionary<ProjectId, ProjectEditor> _editors = new();
    private readonly ConcurrentDictionary<ProjectId, SemaphoreSlim> _saveLocks = new();
    private readonly ConcurrentDictionary<ProjectId, SemaphoreSlim> _editorLocks = new();

    public async Task<T> WithEditorAsync<T>(ProjectId id, Func<ProjectEditor, Task<T>> action, CancellationToken cancellationToken)
    {
        var gate = _editorLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var editor = await GetAsync(id, cancellationToken);
            if (editor is null) throw new KeyNotFoundException($"Project '{id}' was not found.");
            return await action(editor);
        }
        finally { gate.Release(); }
    }

    public async Task<TResult?> UseAsync<TResult>(
        ProjectId id,
        SongProject? update,
        Func<ProjectEditor, TResult> use,
        CancellationToken cancellationToken)
    {
        var gate = _editorLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var editor = update is null
                ? await GetAsync(id, cancellationToken)
                : await SyncCoreAsync(update, cancellationToken);
            if (editor is null) return default;
            return use(editor);
        }
        finally { gate.Release(); }
    }

    public async Task<ProjectEditor?> GetAsync(ProjectId id, CancellationToken cancellationToken)
    {
        if (_editors.TryGetValue(id, out var editor)) return editor;
        var project = await repository.LoadAsync(id, cancellationToken);
        return project is null ? null : _editors.GetOrAdd(id, new ProjectEditor(project));
    }

    public async Task<ProjectEditor?> LoadFromStorageAsync(ProjectId id, CancellationToken cancellationToken)
    {
        var project = await repository.LoadAsync(id, cancellationToken);
        if (project is null) return null;
        var editor = new ProjectEditor(project);
        _editors[id] = editor;
        return editor;
    }

    public async Task<ProjectEditor> CreateAsync(string title, CancellationToken cancellationToken)
    {
        var editor = new ProjectEditor(SongProject.Create(title));
        _editors[editor.Project.Id] = editor;
        await repository.SaveAsync(editor.Project, cancellationToken);
        return editor;
    }

    public async Task<ProjectEditor> ImportAsync(SongProject project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var gate = _editorLocks.GetOrAdd(project.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await repository.ImportAsync(project, cancellationToken);
            var editor = new ProjectEditor(project);
            _editors[project.Id] = editor;
            return editor;
        }
        finally { gate.Release(); }
    }

    public async Task<ProjectEditor?> DuplicateAsync(ProjectId sourceId, CancellationToken cancellationToken)
    {
        var source = await repository.LoadAsync(sourceId, cancellationToken);
        if (source is null) return null;
        var existingTitles = (await repository.ListAsync(cancellationToken))
            .Select(item => item.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copy = PortableProjectImporter.Duplicate(source, AvailableCopyTitle(source.Title, existingTitles));
        return await ImportAsync(copy, cancellationToken);
    }

    public async Task SaveAsync(ProjectEditor editor, CancellationToken cancellationToken) =>
        await repository.SaveAsync(editor.Project, cancellationToken);

    private static string AvailableCopyTitle(string sourceTitle, IReadOnlySet<string> existingTitles)
    {
        for (var number = 1; ; number++)
        {
            var suffix = number == 1 ? " Copy" : $" Copy {number}";
            var maximumBaseLength = 200 - suffix.Length;
            var candidate = sourceTitle[..Math.Min(sourceTitle.Length, maximumBaseLength)].TrimEnd() + suffix;
            if (!existingTitles.Contains(candidate)) return candidate;
        }
    }

    public async Task<ProjectEditor?> SyncAsync(SongProject update, CancellationToken cancellationToken)
    {
        var gate = _editorLocks.GetOrAdd(update.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await SyncCoreAsync(update, cancellationToken);
        }
        finally { gate.Release(); }
    }

    private async Task<ProjectEditor?> SyncCoreAsync(SongProject update, CancellationToken cancellationToken)
    {
        var editor = await GetAsync(update.Id, cancellationToken);
        if (editor is null) return null;

        var project = editor.Project;
        if (!project.Sections.Select(section => section.Id).SequenceEqual(update.Sections.Select(section => section.Id)))
            throw new ArgumentException("Section structure must be changed through section commands.");

        project.Rename(update.Title);
        project.SetArtist(update.Artist);
        project.SetGenre(update.Genre);
        project.SetDescription(update.Description);
        project.SetRawLyricDraft(update.RawLyricDraft);
        project.SetKey(update.Key);
        project.SetTempo(update.Tempo.BeatsPerMinute);
        if (project.TimeSignature.Numerator != update.TimeSignature.Numerator
            || project.TimeSignature.Denominator != update.TimeSignature.Denominator)
            project.SetTimeSignature(update.TimeSignature.Numerator, update.TimeSignature.Denominator);
        foreach (var updatedSection in update.Sections)
        {
            var section = project.FindSection(updatedSection.Id);
            foreach (var updatedLine in updatedSection.LyricLines)
            {
                if (project.IsLyricLineLocked(updatedLine.Id))
                {
                    var current = section.LyricLines.SingleOrDefault(item => item.Id == updatedLine.Id)
                        ?? throw new InvalidOperationException("A locked lyric line cannot be removed.");
                    if (!string.Equals(current.Text, updatedLine.Text, StringComparison.Ordinal))
                        throw new InvalidOperationException("This lyric line is locked. Unlock it before editing the words.");
                }

                foreach (var phrase in updatedLine.Phrases)
                {
                    if (!project.IsPhraseRhythmLocked(updatedLine.Id, phrase.Id)) continue;
                    var current = section.LyricLines.SingleOrDefault(item => item.Id == updatedLine.Id)
                        ?? throw new InvalidOperationException("A locked phrase rhythm cannot be removed.");
                    EnsureLockedPhraseRhythmUnchanged(current, updatedLine, phrase.Id);
                }
            }

            section.SetLyricLines(updatedSection.LyricLines);
            var durationBars = update.Timeline.FindSection(updatedSection.Id).DurationBars;
            if (project.Timeline.FindSection(updatedSection.Id).DurationBars != durationBars)
                project.SetSectionDuration(updatedSection.Id, durationBars);
            project.ReplaceSectionHarmony(updatedSection.Id, updatedSection.Harmony);
        }
        project.ReconcileLocks();
        project.Touch();

        return editor;
    }

    private static void EnsureLockedPhraseRhythmUnchanged(LyricLine current, LyricLine updated, LyricPhraseId phraseId)
    {
        var currentPhrase = current.Phrases.SingleOrDefault(item => item.Id == phraseId)
            ?? throw new InvalidOperationException("A locked phrase rhythm cannot be removed.");
        var updatedPhrase = updated.Phrases.SingleOrDefault(item => item.Id == phraseId)
            ?? throw new InvalidOperationException("A locked phrase rhythm cannot be removed.");
        var currentSyllables = currentPhrase.WordIds
            .SelectMany(wordId => current.Words.Single(word => word.Id == wordId).Syllables.Select(item => item.Id))
            .ToHashSet();
        var updatedSyllables = updatedPhrase.WordIds
            .SelectMany(wordId => updated.Words.Single(word => word.Id == wordId).Syllables.Select(item => item.Id))
            .ToHashSet();
        if (!currentSyllables.SetEquals(updatedSyllables))
            throw new InvalidOperationException("This phrase rhythm is locked. Unlock it before changing its syllables.");

        var currentPlacements = current.SyllablePlacements
            .Where(item => currentSyllables.Contains(item.SyllableId))
            .Select(item => (item.SyllableId, item.Position.Bar, item.Position.Beat, item.Position.Tick))
            .OrderBy(item => item.SyllableId.Value)
            .ToList();
        var updatedPlacements = updated.SyllablePlacements
            .Where(item => updatedSyllables.Contains(item.SyllableId))
            .Select(item => (item.SyllableId, item.Position.Bar, item.Position.Beat, item.Position.Tick))
            .OrderBy(item => item.SyllableId.Value)
            .ToList();
        if (!currentPlacements.SequenceEqual(updatedPlacements))
            throw new InvalidOperationException("This phrase rhythm is locked. Unlock it before changing placements.");
    }

    public async Task<ProjectEditor?> UpdateAsync(
        SongProject update,
        DateTimeOffset expectedLastModifiedUtc,
        CancellationToken cancellationToken)
    {
        var saveLock = _saveLocks.GetOrAdd(update.Id, _ => new SemaphoreSlim(1, 1));
        await saveLock.WaitAsync(cancellationToken);
        try
        {
            var persisted = await repository.LoadAsync(update.Id, cancellationToken);
            if (persisted is null) return null;
            if (persisted.LastModifiedUtc != expectedLastModifiedUtc)
                throw new StaleProjectSessionException();
            var editor = await SyncAsync(update, cancellationToken);
            if (editor is not null)
                await repository.SaveAsync(editor.Project, cancellationToken);
            return editor;
        }
        finally { saveLock.Release(); }
    }

    public async Task<ProjectRecoverySnapshot?> LoadRecoveryAsync(ProjectId id, CancellationToken cancellationToken)
    {
        var snapshot = await repository.LoadRecoverySnapshotAsync(id, cancellationToken);
        if (snapshot is null) return null;
        _editors[id] = new ProjectEditor(snapshot.Project);
        return snapshot;
    }
}
