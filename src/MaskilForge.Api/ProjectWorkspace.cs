using System.Collections.Concurrent;
using System.Security.Cryptography;
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

    public async Task<ProjectEditor> CreateFromLyricCaptureAsync(
        string title,
        string artist,
        SongGenre genre,
        string description,
        string rawLyricDraft,
        CancellationToken cancellationToken)
    {
        var project = SongProject.Create(title);
        project.SetArtist(artist);
        project.SetGenre(genre);
        project.SetDescription(description);
        project.SetRawLyricDraft(rawLyricDraft);
        var editor = new ProjectEditor(project);
        _editors[project.Id] = editor;
        await repository.SaveAsync(project, cancellationToken);
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

    public async Task<ProjectEditor> ImportWithAssetsAsync(
        SongProject project,
        IReadOnlyDictionary<ProjectAssetId, byte[]> assets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(assets);
        var gate = _editorLocks.GetOrAdd(project.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await repository.ImportWithAssetsAsync(project, assets, cancellationToken);
            var editor = new ProjectEditor(project);
            _editors[project.Id] = editor;
            return editor;
        }
        finally { gate.Release(); }
    }

    public async Task<ProjectEditor?> AddOriginalVocalTakeAsync(
        ProjectId id,
        DateTimeOffset expectedLastModifiedUtc,
        string mediaType,
        byte[] content,
        DateTimeOffset createdUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var saveLock = _saveLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await saveLock.WaitAsync(cancellationToken);
        try
        {
            var editorLock = _editorLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
            await editorLock.WaitAsync(cancellationToken);
            try
            {
                var project = await repository.LoadAsync(id, cancellationToken);
                if (project is null) return null;
                if (project.LastModifiedUtc != expectedLastModifiedUtc)
                    throw new StaleProjectSessionException();

                var asset = new ProjectAsset(
                    ProjectAssetId.New(),
                    ProjectAssetKind.OriginalVocalTake,
                    mediaType,
                    content.LongLength,
                    Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                    createdUtc,
                    NextAvailableTakeName(project));
                project.RegisterAsset(asset);
                await repository.SaveWithAssetAsync(project, asset, new MemoryStream(content, writable: false), cancellationToken);

                var editor = new ProjectEditor(project);
                _editors[id] = editor;
                return editor;
            }
            finally { editorLock.Release(); }
        }
        finally { saveLock.Release(); }
    }

    public async Task<ProjectEditor?> RemoveOriginalVocalTakeAsync(
        ProjectId id,
        ProjectAssetId assetId,
        DateTimeOffset expectedLastModifiedUtc,
        CancellationToken cancellationToken)
    {
        var saveLock = _saveLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await saveLock.WaitAsync(cancellationToken);
        try
        {
            var editorLock = _editorLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
            await editorLock.WaitAsync(cancellationToken);
            try
            {
                var project = await repository.LoadAsync(id, cancellationToken);
                if (project is null) return null;
                if (project.LastModifiedUtc != expectedLastModifiedUtc)
                    throw new StaleProjectSessionException();

                var asset = project.Assets.SingleOrDefault(item => item.Id == assetId && item.Kind == ProjectAssetKind.OriginalVocalTake)
                    ?? throw new KeyNotFoundException($"Rough vocal take '{assetId}' was not found.");
                project.RemoveAsset(assetId);
                await repository.SaveWithoutAssetAsync(project, asset, cancellationToken);

                var editor = new ProjectEditor(project);
                _editors[id] = editor;
                return editor;
            }
            finally { editorLock.Release(); }
        }
        finally { saveLock.Release(); }
    }

    public async Task<ProjectEditor?> RenameOriginalVocalTakeAsync(
        ProjectId id,
        ProjectAssetId assetId,
        string name,
        DateTimeOffset expectedLastModifiedUtc,
        CancellationToken cancellationToken)
    {
        var saveLock = _saveLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await saveLock.WaitAsync(cancellationToken);
        try
        {
            var editorLock = _editorLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
            await editorLock.WaitAsync(cancellationToken);
            try
            {
                var project = await repository.LoadAsync(id, cancellationToken);
                if (project is null) return null;
                if (project.LastModifiedUtc != expectedLastModifiedUtc)
                    throw new StaleProjectSessionException();

                if (!project.Assets.Any(item => item.Id == assetId && item.Kind == ProjectAssetKind.OriginalVocalTake))
                    throw new KeyNotFoundException($"Rough vocal take '{assetId}' was not found.");
                project.RenameAsset(assetId, name);
                await repository.SaveAsync(project, cancellationToken);

                var editor = new ProjectEditor(project);
                _editors[id] = editor;
                return editor;
            }
            finally { editorLock.Release(); }
        }
        finally { saveLock.Release(); }
    }

    public async Task<ProjectEditor?> ReplaceLoudnessObservationsAsync(
        ProjectId id,
        ProjectAssetId assetId,
        DateTimeOffset expectedLastModifiedUtc,
        IReadOnlyList<LoudnessFrameReport> frames,
        DateTimeOffset createdUtc,
        CancellationToken cancellationToken)
    {
        var observations = LoudnessObservationReport.CreateObservations(assetId, frames, createdUtc);
        return await ReplacePerformanceObservationsAsync(
            id,
            assetId,
            expectedLastModifiedUtc,
            LoudnessObservationReport.AnalyzerId,
            LoudnessObservationReport.ObservationKind,
            observations,
            cancellationToken);
    }

    public async Task<ProjectEditor?> ReplacePitchObservationsAsync(
        ProjectId id,
        ProjectAssetId assetId,
        DateTimeOffset expectedLastModifiedUtc,
        IReadOnlyList<PitchFrameReport> frames,
        DateTimeOffset createdUtc,
        CancellationToken cancellationToken)
    {
        var observations = PitchObservationReport.CreateObservations(assetId, frames, createdUtc);
        return await ReplacePerformanceObservationsAsync(
            id,
            assetId,
            expectedLastModifiedUtc,
            PitchObservationReport.AnalyzerId,
            PitchObservationReport.ObservationKind,
            observations,
            cancellationToken);
    }

    private async Task<ProjectEditor?> ReplacePerformanceObservationsAsync(
        ProjectId id,
        ProjectAssetId assetId,
        DateTimeOffset expectedLastModifiedUtc,
        string analyzerId,
        string observationKind,
        IReadOnlyList<PerformanceObservation> observations,
        CancellationToken cancellationToken)
    {
        var saveLock = _saveLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await saveLock.WaitAsync(cancellationToken);
        try
        {
            var editorLock = _editorLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
            await editorLock.WaitAsync(cancellationToken);
            try
            {
                var project = await repository.LoadAsync(id, cancellationToken);
                if (project is null) return null;
                if (project.LastModifiedUtc != expectedLastModifiedUtc)
                    throw new StaleProjectSessionException();

                project.ReplacePerformanceObservations(
                    assetId,
                    analyzerId,
                    observationKind,
                    observations);
                await repository.SaveAsync(project, cancellationToken);

                var editor = new ProjectEditor(project);
                _editors[id] = editor;
                return editor;
            }
            finally { editorLock.Release(); }
        }
        finally { saveLock.Release(); }
    }

    public async Task<ProjectEditor?> DuplicateAsync(ProjectId sourceId, CancellationToken cancellationToken)
    {
        var source = await repository.LoadAsync(sourceId, cancellationToken);
        if (source is null) return null;
        var existingTitles = (await repository.ListAsync(cancellationToken))
            .Select(item => item.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copy = PortableProjectImporter.Duplicate(source, AvailableCopyTitle(source.Title, existingTitles));
        if (source.Assets.Count == 0) return await ImportAsync(copy, cancellationToken);

        var assets = new Dictionary<ProjectAssetId, byte[]>();
        foreach (var asset in source.Assets)
        {
            await using var stream = await repository.OpenAssetAsync(source.Id, asset.Id, cancellationToken)
                ?? throw new InvalidProjectDataException($"Project asset '{asset.Id}' is missing.");
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            assets[asset.Id] = buffer.ToArray();
        }
        return await ImportWithAssetsAsync(copy, assets, cancellationToken);
    }

    private static string NextAvailableTakeName(SongProject project)
    {
        var names = project.Assets
            .Where(asset => asset.Kind == ProjectAssetKind.OriginalVocalTake)
            .Select(asset => asset.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var takeNumber = 1; ; takeNumber++)
        {
            var candidate = $"Take {takeNumber}";
            if (!names.Contains(candidate)) return candidate;
        }
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
