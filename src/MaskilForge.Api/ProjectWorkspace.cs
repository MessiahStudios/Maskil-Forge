using System.Collections.Concurrent;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Api;

public sealed class ProjectWorkspace(IProjectRepository repository)
{
    private readonly ConcurrentDictionary<ProjectId, ProjectEditor> _editors = new();
    private readonly ConcurrentDictionary<ProjectId, SemaphoreSlim> _saveLocks = new();

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

    public async Task SaveAsync(ProjectEditor editor, CancellationToken cancellationToken) =>
        await repository.SaveAsync(editor.Project, cancellationToken);

    public async Task<ProjectEditor?> SyncAsync(SongProject update, CancellationToken cancellationToken)
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
        project.SetTempo(update.Tempo.BeatsPerMinute);
        project.SetTimeSignature(update.TimeSignature.Numerator, update.TimeSignature.Denominator);
        foreach (var updatedSection in update.Sections)
        {
            var section = project.FindSection(updatedSection.Id);
            section.SetLyricLines(updatedSection.LyricLines);
            var durationBars = update.Timeline.FindSection(updatedSection.Id).DurationBars;
            if (project.Timeline.FindSection(updatedSection.Id).DurationBars != durationBars)
                project.SetSectionDuration(updatedSection.Id, durationBars);
        }
        project.Touch();

        return editor;
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
