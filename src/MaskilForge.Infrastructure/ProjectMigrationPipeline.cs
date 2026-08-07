using System.Text.Json;
using System.Text.Json.Nodes;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Infrastructure;

internal interface IProjectMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    JsonObject Apply(JsonObject project);
}

internal sealed class ProjectMigrationPipeline(IEnumerable<IProjectMigration>? migrations = null)
{
    private readonly IReadOnlyDictionary<int, IProjectMigration> _migrations =
        (migrations ?? []).ToDictionary(migration => migration.FromVersion);

    public JsonObject Normalize(JsonObject project)
    {
        var version = ReadVersion(project);
        if (version > SchemaVersion.Current.Value)
            throw new UnsupportedProjectSchemaException(version, SchemaVersion.Current.Value);
        if (version < 1)
            throw new InvalidProjectDataException($"Schema version {version} is invalid.");

        while (version < SchemaVersion.Current.Value)
        {
            if (!_migrations.TryGetValue(version, out var migration))
                throw new InvalidProjectDataException($"No migration is registered from schema version {version}.");
            project = migration.Apply(project);
            version = migration.ToVersion;
        }

        project["schemaVersion"] = SchemaVersion.Current.Value;
        return project;
    }

    private static int ReadVersion(JsonObject project)
    {
        if (!project.TryGetPropertyValue("schemaVersion", out var node) || node is null)
            throw new InvalidProjectDataException("The project does not declare a schema version.");
        if (node is JsonValue scalar && scalar.TryGetValue<int>(out var numeric)) return numeric;
        if (node is JsonObject legacy && legacy["value"] is JsonValue value && value.TryGetValue<int>(out var objectVersion))
            return objectVersion;
        throw new InvalidProjectDataException("The project schema version is malformed.");
    }

    public static JsonObject Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidProjectDataException("The project root must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new CorruptProjectException("The project contains malformed JSON.", null, exception);
        }
    }
}
