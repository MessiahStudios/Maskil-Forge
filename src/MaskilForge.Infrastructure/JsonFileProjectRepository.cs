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

    private string GetPath(ProjectId id) => Path.Combine(_directory, $"{id}.json");
}
