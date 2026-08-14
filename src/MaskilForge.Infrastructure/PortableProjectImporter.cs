using System.Text.Json;
using System.Text.Json.Serialization;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Infrastructure;

/// <summary>
/// Parses, migrates, and validates an artist-owned portable project document.
/// Persistence and identity-collision policy remain repository concerns.
/// </summary>
public static class PortableProjectImporter
{
    private static readonly ProjectMigrationPipeline MigrationPipeline = ProjectMigrationPipeline.CreateCurrent();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static SongProject Import(string json) => Inspect(json).Project;

    public static SongProject ImportAsCopy(string json) => Inspect(json, true).Project;

    public static PortableProjectDocument Inspect(string json, bool importAsCopy = false)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidProjectDataException("The portable project file is empty.");

        try
        {
            var parsed = ProjectMigrationPipeline.Parse(json);
            var sourceSchemaVersion = ProjectMigrationPipeline.ReadVersion(parsed);
            var normalized = MigrationPipeline.Normalize(parsed);
            if (importAsCopy) ApplyCopyIdentity(normalized);
            var project = normalized.Deserialize<SongProject>(JsonOptions)
                ?? throw new InvalidProjectDataException("The portable project file contains no project data.");
            return new PortableProjectDocument(project, sourceSchemaVersion);
        }
        catch (ProjectPersistenceException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidProjectDataException("The portable project file contains invalid project data.", null, exception);
        }
    }

    private static void ApplyCopyIdentity(System.Text.Json.Nodes.JsonObject project)
    {
        const string suffix = " (Imported Copy)";
        var title = project["title"]?.GetValue<string>()?.Trim() ?? "Imported Song";
        if (!title.EndsWith(suffix, StringComparison.Ordinal))
        {
            var maximumBaseLength = 200 - suffix.Length;
            title = title[..Math.Min(title.Length, maximumBaseLength)].TrimEnd() + suffix;
        }
        var importedAtUtc = DateTimeOffset.UtcNow;
        project["id"] = ProjectId.New().ToString();
        project["title"] = title;
        project["createdUtc"] = importedAtUtc;
        project["lastModifiedUtc"] = importedAtUtc;
    }
}

public sealed record PortableProjectDocument(SongProject Project, int SourceSchemaVersion);
