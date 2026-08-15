using System.Text.Json;
using System.Text.Json.Serialization;
using MaskilForge.Domain;

namespace MaskilForge.Infrastructure;

/// <summary>
/// Exports the authoritative Song Graph as a portable, versioned project file.
/// The file contains no repository paths, recovery state, or command history.
/// </summary>
public static class PortableProjectExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public const string ContentType = "application/vnd.maskil-forge.project+json";

    public static byte[] SerializeDocument(SongProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return JsonSerializer.SerializeToUtf8Bytes(project, JsonOptions);
    }

    public static byte[] Export(SongProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Assets.Count > 0)
            throw new InvalidOperationException(
                "This project references external media. Legacy .maskil.json export cannot carry original recordings; export an asset-owning .maskil package instead.");
        return SerializeDocument(project);
    }
}
