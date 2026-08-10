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

internal sealed class V1ToV2ProjectMigration : IProjectMigration
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public JsonObject Apply(JsonObject project)
    {
        var tempo = project["tempo"]?.DeepClone()
            ?? throw new InvalidProjectDataException("Schema-v1 project is missing tempo data.");
        var timeSignature = project["timeSignature"]?.DeepClone()
            ?? throw new InvalidProjectDataException("Schema-v1 project is missing time-signature data.");
        var placements = new JsonArray();
        var startBar = 1;
        if (project["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
            {
                var sectionId = section["id"]?.DeepClone()
                    ?? throw new InvalidProjectDataException("Schema-v1 section is missing its ID.");
                placements.Add(new JsonObject
                {
                    ["sectionId"] = sectionId,
                    ["start"] = new JsonObject { ["bar"] = startBar, ["beat"] = 1, ["tick"] = 0 },
                    ["durationBars"] = 8
                });
                startBar += 8;
            }
        }

        project["timeline"] = new JsonObject
        {
            ["ticksPerQuarterNote"] = TimelineResolution.TicksPerQuarterNote,
            ["tempoMap"] = new JsonObject { ["events"] = new JsonArray(tempo) },
            ["timeSignatureMap"] = new JsonObject { ["events"] = new JsonArray(timeSignature) },
            ["sectionPlacements"] = placements
        };
        project.Remove("tempo");
        project.Remove("timeSignature");
        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V2ToV3ProjectMigration : IProjectMigration
{
    public int FromVersion => 2;
    public int ToVersion => 3;

    public JsonObject Apply(JsonObject project)
    {
        if (project["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
            {
                if (section["lyricLines"] is not JsonArray lines) continue;
                foreach (var line in lines.OfType<JsonObject>())
                {
                    var idText = line["id"]?.GetValue<string>();
                    if (!Guid.TryParse(idText, out var id))
                        throw new InvalidProjectDataException("Schema-v2 lyric line is missing a valid ID.");
                    var lineId = new LyricLineId(id);
                    var text = line["text"]?.GetValue<string>() ?? string.Empty;
                    var words = new JsonArray();
                    var tokens = LyricLine.Tokenize(text);
                    for (var index = 0; index < tokens.Count; index++)
                    {
                        var token = tokens[index];
                        words.Add(new JsonObject
                        {
                            ["id"] = LyricLine.CreateMigratedWordId(lineId, index, token.Text).ToString(),
                            ["text"] = token.Text,
                            ["start"] = token.Start,
                            ["length"] = token.Length,
                            ["syllables"] = new JsonArray()
                        });
                    }
                    line["words"] = words;
                }
            }
        }

        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V3ToV4ProjectMigration : IProjectMigration
{
    public int FromVersion => 3;
    public int ToVersion => 4;

    public JsonObject Apply(JsonObject project)
    {
        if (project["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
            {
                if (section["lyricLines"] is not JsonArray lines) continue;
                foreach (var line in lines.OfType<JsonObject>())
                {
                    if (line["words"] is not JsonArray words) continue;
                    foreach (var word in words.OfType<JsonObject>())
                    {
                        if (word["syllables"] is not JsonArray syllables)
                        {
                            word["syllables"] = new JsonArray();
                            continue;
                        }

                        var position = 0;
                        foreach (var syllable in syllables.OfType<JsonObject>())
                        {
                            syllable["position"] = position++;
                            syllable["source"] = nameof(SyllableSource.Manual);
                        }
                    }
                }
            }
        }

        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V4ToV5ProjectMigration : IProjectMigration
{
    public int FromVersion => 4;
    public int ToVersion => 5;

    public JsonObject Apply(JsonObject project)
    {
        if (project["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
            {
                if (section["lyricLines"] is not JsonArray lines) continue;
                foreach (var line in lines.OfType<JsonObject>())
                {
                    var idText = line["id"]?.GetValue<string>();
                    if (!Guid.TryParse(idText, out var id))
                        throw new InvalidProjectDataException("Schema-v4 lyric line is missing a valid ID.");
                    var lineId = new LyricLineId(id);
                    var text = line["text"]?.GetValue<string>() ?? string.Empty;
                    var punctuation = new JsonArray();
                    var punctuationTokens = LyricLine.TokenizePunctuation(text);
                    for (var index = 0; index < punctuationTokens.Count; index++)
                    {
                        var token = punctuationTokens[index];
                        punctuation.Add(new JsonObject
                        {
                            ["id"] = LyricLine.CreateMigratedPunctuationId(lineId, index, token.Text).ToString(),
                            ["text"] = token.Text,
                            ["start"] = token.Start,
                            ["length"] = token.Length
                        });
                    }
                    line["punctuation"] = punctuation;

                    var wordIds = new JsonArray();
                    if (line["words"] is JsonArray words)
                    {
                        foreach (var word in words.OfType<JsonObject>())
                        {
                            var wordId = word["id"]?.GetValue<string>();
                            if (!Guid.TryParse(wordId, out _))
                                throw new InvalidProjectDataException("Schema-v4 lyric word is missing a valid ID.");
                            wordIds.Add(wordId);
                        }
                    }

                    var phrases = new JsonArray();
                    if (wordIds.Count > 0)
                    {
                        phrases.Add(new JsonObject
                        {
                            ["id"] = LyricLine.CreateMigratedPhraseId(lineId, 0).ToString(),
                            ["position"] = 0,
                            ["wordIds"] = wordIds,
                            ["source"] = nameof(PhraseSource.Default)
                        });
                    }
                    line["phrases"] = phrases;
                }
            }
        }

        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V5ToV6ProjectMigration : IProjectMigration
{
    public int FromVersion => 5;
    public int ToVersion => 6;

    public JsonObject Apply(JsonObject project)
    {
        if (project["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
            {
                if (section["lyricLines"] is not JsonArray lines) continue;
                foreach (var line in lines.OfType<JsonObject>())
                {
                    if (line["words"] is not JsonArray words) continue;
                    foreach (var word in words.OfType<JsonObject>())
                    {
                        if (word["syllables"] is not JsonArray syllables) continue;
                        foreach (var syllable in syllables.OfType<JsonObject>())
                            syllable["stress"] = null;
                    }
                }
            }
        }

        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V6ToV7ProjectMigration : IProjectMigration
{
    public int FromVersion => 6;
    public int ToVersion => 7;

    public JsonObject Apply(JsonObject project)
    {
        if (project["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
            {
                if (section["lyricLines"] is not JsonArray lines) continue;
                foreach (var line in lines.OfType<JsonObject>())
                {
                    if (line["phrases"] is not JsonArray phrases) continue;
                    foreach (var phrase in phrases.OfType<JsonObject>())
                        phrase["prosody"] = null;
                }
            }
        }

        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V7ToV8ProjectMigration : IProjectMigration
{
    public int FromVersion => 7;
    public int ToVersion => 8;

    public JsonObject Apply(JsonObject project)
    {
        if (project["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
            {
                if (section["lyricLines"] is not JsonArray lines) continue;
                foreach (var line in lines.OfType<JsonObject>())
                    line["syllablePlacements"] = new JsonArray();
            }
        }

        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V8ToV9ProjectMigration : IProjectMigration
{
    public int FromVersion => 8;
    public int ToVersion => 9;

    public JsonObject Apply(JsonObject project)
    {
        if (project["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
            {
                if (section["lyricLines"] is not JsonArray lines) continue;
                foreach (var line in lines.OfType<JsonObject>())
                    line["rhythmCandidates"] = new JsonArray();
            }
        }

        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V9ToV10ProjectMigration : IProjectMigration
{
    public int FromVersion => 9;
    public int ToVersion => 10;

    public JsonObject Apply(JsonObject project)
    {
        if (project["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
            {
                if (section["lyricLines"] is not JsonArray lines) continue;
                foreach (var line in lines.OfType<JsonObject>())
                    line["breathPoints"] = new JsonArray();
            }
        }

        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V10ToV11ProjectMigration : IProjectMigration
{
    public int FromVersion => 10;
    public int ToVersion => 11;

    public JsonObject Apply(JsonObject project)
    {
        project["locks"] = new JsonArray();
        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V11ToV12ProjectMigration : IProjectMigration
{
    public int FromVersion => 11;
    public int ToVersion => 12;

    public JsonObject Apply(JsonObject project)
    {
        project["key"] = new JsonObject
        {
            ["tonic"] = "C",
            ["accidental"] = "Natural",
            ["mode"] = "Major"
        };
        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V12ToV13ProjectMigration : IProjectMigration
{
    public int FromVersion => 12;
    public int ToVersion => 13;

    public JsonObject Apply(JsonObject project)
    {
        if (project["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
                section["harmony"] = new JsonArray();
        }

        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V13ToV14ProjectMigration : IProjectMigration
{
    public int FromVersion => 13;
    public int ToVersion => 14;

    public JsonObject Apply(JsonObject project)
    {
        if (project["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
                section["harmonyCandidates"] = new JsonArray();
        }

        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V14ToV15ProjectMigration : IProjectMigration
{
    public int FromVersion => 14;
    public int ToVersion => 15;

    public JsonObject Apply(JsonObject project)
    {
        if (project["sections"] is JsonArray sections)
            foreach (var section in sections.OfType<JsonObject>())
                if (section["harmony"] is JsonArray harmony)
                    foreach (var chord in harmony.OfType<JsonObject>()) chord["voicing"] = null;
        project["schemaVersion"] = ToVersion;
        return project;
    }
}

internal sealed class V15ToV16ProjectMigration : IProjectMigration
{
    public int FromVersion => 15;
    public int ToVersion => 16;

    public JsonObject Apply(JsonObject project)
    {
        project["arrangement"] = new JsonArray();
        project["schemaVersion"] = ToVersion;
        return project;
    }
}
