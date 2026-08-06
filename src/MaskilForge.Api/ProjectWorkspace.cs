using System.Collections.Concurrent;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Api;

public sealed class ProjectWorkspace(IProjectRepository repository)
{
    private readonly ConcurrentDictionary<ProjectId, ProjectEditor> _editors = new();

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

    public async Task<ProjectEditor?> UpdateAsync(SongProject update, CancellationToken cancellationToken)
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
        project.SetTempo(update.Tempo.BeatsPerMinute);
        project.SetTimeSignature(update.TimeSignature.Numerator, update.TimeSignature.Denominator);
        foreach (var updatedSection in update.Sections)
        {
            var section = project.FindSection(updatedSection.Id);
            section.SetLyricLines(updatedSection.LyricLines);
        }

        await repository.SaveAsync(project, cancellationToken);
        return editor;
    }
}
