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

    public static SongProject Duplicate(SongProject project, string title)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A copy title is required.", nameof(title));
        var json = System.Text.Encoding.UTF8.GetString(PortableProjectExporter.Export(project));
        var document = ProjectMigrationPipeline.Parse(json);
        ApplyCopyIdentity(document, title.Trim());
        return Import(document.ToJsonString());
    }

    public static PortableProjectDocument Inspect(string json, bool importAsCopy = false)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidProjectDataException("The portable project file is empty.");

        try
        {
            var parsed = ProjectMigrationPipeline.Parse(json);
            var sourceSchemaVersion = ProjectMigrationPipeline.ReadVersion(parsed);
            var normalized = MigrationPipeline.Normalize(parsed);
            if (importAsCopy) ApplyCopyIdentity(normalized, ImportedCopyTitle(normalized));
            var project = normalized.Deserialize<SongProject>(JsonOptions)
                ?? throw new InvalidProjectDataException("The portable project file contains no project data.");
            if (project.Assets.Count > 0)
                throw new InvalidProjectDataException(
                    "This .maskil.json file references external media without carrying its bytes. Import an asset-owning project package instead.");
            return new PortableProjectDocument(project, sourceSchemaVersion);
        }
        catch (ProjectPersistenceException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidProjectDataException("The portable project file contains invalid project data.", null, exception);
        }
    }

    private static string ImportedCopyTitle(System.Text.Json.Nodes.JsonObject project)
    {
        const string suffix = " (Imported Copy)";
        var title = project["title"]?.GetValue<string>()?.Trim() ?? "Imported Song";
        if (title.EndsWith(suffix, StringComparison.Ordinal)) return title;
        var maximumBaseLength = 200 - suffix.Length;
        return title[..Math.Min(title.Length, maximumBaseLength)].TrimEnd() + suffix;
    }

    private static void ApplyCopyIdentity(System.Text.Json.Nodes.JsonObject project, string title)
    {
        var copiedAtUtc = DateTimeOffset.UtcNow;
        project["id"] = ProjectId.New().ToString();
        project["title"] = title;
        project["createdUtc"] = copiedAtUtc;
        project["lastModifiedUtc"] = copiedAtUtc;
    }
}

public sealed record PortableProjectDocument(SongProject Project, int SourceSchemaVersion);
