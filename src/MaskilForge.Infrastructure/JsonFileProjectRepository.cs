using System.Text.Json;
using System.Text.Json.Serialization;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Infrastructure;

public sealed class JsonFileProjectRepository(string directory) : IProjectRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _directory = string.IsNullOrWhiteSpace(directory)
        ? throw new ArgumentException("A persistence directory is required.", nameof(directory))
        : Path.GetFullPath(directory);

    public async Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory)) return [];
        var summaries = new List<ProjectSummary>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(path);
            var project = await JsonSerializer.DeserializeAsync<SongProject>(stream, JsonOptions, cancellationToken);
            if (project is not null)
                summaries.Add(new ProjectSummary(project.Id, project.Title, project.Artist, project.Genre, project.LastModifiedUtc, project.Sections.Count, !string.IsNullOrWhiteSpace(project.RawLyricDraft)));
        }
        return summaries.OrderByDescending(project => project.LastModifiedUtc).ToList();
    }

    public async Task<IReadOnlyList<TrashedProjectSummary>> ListTrashAsync(CancellationToken cancellationToken = default)
    {
        var trashDirectory = GetTrashDirectory();
        if (!Directory.Exists(trashDirectory)) return [];
        var summaries = new List<TrashedProjectSummary>();
        foreach (var path in Directory.EnumerateFiles(trashDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(path);
            var project = await JsonSerializer.DeserializeAsync<SongProject>(stream, JsonOptions, cancellationToken);
            if (project is not null)
                summaries.Add(new TrashedProjectSummary(project.Id, project.Title, project.Artist, File.GetLastWriteTimeUtc(path)));
        }
        return summaries.OrderByDescending(project => project.DeletedAtUtc).ToList();
    }

    public async Task<SongProject?> LoadAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        var path = GetPath(id);
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SongProject>(stream, JsonOptions, cancellationToken);
    }

    public async Task SaveAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        Directory.CreateDirectory(_directory);
        var path = GetPath(project.Id);
        var temporaryPath = path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, project, JsonOptions, cancellationToken);
        }
        File.Move(temporaryPath, path, true);
    }

    public Task<bool> MoveToTrashAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = GetPath(id);
        if (!File.Exists(source)) return Task.FromResult(false);
        var trashDirectory = GetTrashDirectory();
        Directory.CreateDirectory(trashDirectory);
        var destination = Path.Combine(trashDirectory, $"{id}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json");
        File.Move(source, destination);
        return Task.FromResult(true);
    }

    public Task<bool> RestoreFromTrashAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = FindTrashPath(id);
        if (source is null) return Task.FromResult(false);
        Directory.CreateDirectory(_directory);
        var destination = GetPath(id);
        if (File.Exists(destination)) throw new InvalidOperationException("A project with this ID already exists in the song library.");
        File.Move(source, destination);
        return Task.FromResult(true);
    }

    public Task<bool> PermanentlyDeleteAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = FindTrashPath(id);
        if (path is null) return Task.FromResult(false);
        File.Delete(path);
        return Task.FromResult(true);
    }

    private string GetTrashDirectory() => Path.Combine(_directory, "trash");
    private string? FindTrashPath(ProjectId id) => Directory.Exists(GetTrashDirectory())
        ? Directory.EnumerateFiles(GetTrashDirectory(), $"{id}-*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
        : null;

    private string GetPath(ProjectId id) => Path.Combine(_directory, $"{id}.json");
}
