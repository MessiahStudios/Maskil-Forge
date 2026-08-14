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

    public static SongProject Import(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidProjectDataException("The portable project file is empty.");

        try
        {
            var normalized = MigrationPipeline.Normalize(ProjectMigrationPipeline.Parse(json));
            return normalized.Deserialize<SongProject>(JsonOptions)
                ?? throw new InvalidProjectDataException("The portable project file contains no project data.");
        }
        catch (ProjectPersistenceException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidProjectDataException("The portable project file contains invalid project data.", null, exception);
        }
    }
}
