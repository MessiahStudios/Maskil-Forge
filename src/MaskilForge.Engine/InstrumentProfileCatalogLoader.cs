using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaskilForge.Domain;

namespace MaskilForge.Engine;

public static class InstrumentProfileCatalogLoader
{
    public const int CurrentVersion = 2;
    public const string ResourceName = "MaskilForge.Engine.Knowledge.instrument-profiles.v2.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Lazy<InstrumentProfileCatalog> Loaded = new(LoadCurrent);

    public static InstrumentProfileCatalog Current => Loaded.Value;

    public static InstrumentProfileCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var catalog = JsonSerializer.Deserialize<InstrumentProfileCatalog>(stream, JsonOptions)
            ?? throw new InvalidOperationException("The instrument-profile catalog is empty.");
        if (catalog.Version != CurrentVersion)
            throw new InvalidOperationException($"Instrument-profile catalog version {catalog.Version} is not supported. Expected {CurrentVersion}.");
        return catalog;
    }

    public static InstrumentProfileCatalog Load(string json)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return Load(stream);
    }

    private static InstrumentProfileCatalog LoadCurrent()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Instrument-profile catalog '{ResourceName}' was not embedded.");
        return Load(stream);
    }
}
