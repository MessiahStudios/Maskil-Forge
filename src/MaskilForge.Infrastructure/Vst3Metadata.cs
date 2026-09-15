using System.Security.Cryptography;
using System.Text.Json;

namespace MaskilForge.Infrastructure;

public sealed record Vst3ReportedClass(string Id, string Name, string Category, string? Vendor,
    string? Version, string? SdkVersion, IReadOnlyList<string> SubCategories);
public sealed record Vst3ReportedModule(string Name, string Version, string Vendor, IReadOnlyList<Vst3ReportedClass> Classes);
public sealed record Vst3MetadataInspection(string Status, string? Source = null, string? Sha256 = null, Vst3ReportedModule? Module = null);

/// <summary>One instance per discovery scan. Reads bounded optional manifests, never executable modules.</summary>
public sealed class Vst3MetadataReader(int maxFiles = 64)
{
    public const int MaxFileBytes = 128 * 1024;
    private int _remainingFiles = maxFiles >= 0 ? maxFiles : throw new ArgumentOutOfRangeException(nameof(maxFiles));
    public Vst3MetadataInspection Read(string bundle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Prefer current metadata. Only absence permits legacy fallback; invalid or linked current data must remain visible.
        foreach (var source in new[] { "Contents/Resources/moduleinfo.json", "Contents/moduleinfo.json" })
        {
            try
            {
                var path = bundle;
                var segments = source.Split('/');
                var missing = false;
                var bundleAttributes = File.GetAttributes(bundle);
                if (bundleAttributes.HasFlag(FileAttributes.ReparsePoint)) return new("LinkedPath", source);
                if (!bundleAttributes.HasFlag(FileAttributes.Directory)) return new("Unreadable", source);
                for (var index = 0; index < segments.Length; index++)
                {
                    path = Path.Combine(path, segments[index]);
                    FileAttributes attributes;
                    try { attributes = File.GetAttributes(path); }
                    catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException) { missing = true; break; }
                    if (attributes.HasFlag(FileAttributes.ReparsePoint)) return new("LinkedPath", source);
                    if (attributes.HasFlag(FileAttributes.Directory) != (index < segments.Length - 1)) return new("Unreadable", source);
                }
                if (missing) continue;
                if (_remainingFiles == 0) return new("ScanLimit", source);
                _remainingFiles--;
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length > MaxFileBytes) return new("TooLarge", source);
                var bytes = new byte[MaxFileBytes + 1];
                var count = 0;
                while (count < bytes.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = stream.Read(bytes, count, Math.Min(16 * 1024, bytes.Length - count));
                    if (read == 0) break;
                    count += read;
                }
                if (count > MaxFileBytes) return new("TooLarge", source);
                var content = bytes.AsMemory(0, count);
                var digest = Convert.ToHexString(SHA256.HashData(content.Span));
                // Accept UTF-8 JSON with comments/trailing commas, as emitted by SDK examples. Other JSON5 syntax is reported as unsupported.
                if (content.Span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) content = content[3..];
                using var document = JsonDocument.Parse(content, new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 16 });
                ValidateUniqueProperties(document.RootElement);
                var root = document.RootElement;
                var name = RequiredText(root, "Name");
                var version = RequiredText(root, "Version");
                var vendor = RequiredText(root.GetProperty("Factory Info"), "Vendor");
                var classes = root.GetProperty("Classes");
                if (classes.ValueKind != JsonValueKind.Array || classes.GetArrayLength() > 128) throw new JsonException();
                var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var reported = new List<Vst3ReportedClass>();
                foreach (var item in classes.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = RequiredText(item, "CID");
                    if (id.Length != 32 || !id.All(char.IsAsciiHexDigit) || !identities.Add(id)) throw new JsonException();
                    var categories = new List<string>();
                    if (item.TryGetProperty("Sub Categories", out var subCategories))
                    {
                        if (subCategories.ValueKind != JsonValueKind.Array || subCategories.GetArrayLength() > 16) throw new JsonException();
                        foreach (var category in subCategories.EnumerateArray()) categories.Add(Text(category));
                    }
                    reported.Add(new(id.ToUpperInvariant(), RequiredText(item, "Name"), RequiredText(item, "Category"),
                        OptionalText(item, "Vendor"), OptionalText(item, "Version"), OptionalText(item, "SDKVersion"), categories.AsReadOnly()));
                }
                return new("Available", source, digest, new(name, version, vendor, reported.AsReadOnly()));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return new("Unreadable", source); }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or System.Text.DecoderFallbackException)
            { return new("InvalidOrUnsupported", source); }
        }
        return new("Missing");
    }

    private static string RequiredText(JsonElement element, string property) => Text(element.GetProperty(property));
    private static string? OptionalText(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind == JsonValueKind.String && value.GetString() == "") return null;
        return Text(value);
    }
    private static string Text(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String) throw new JsonException();
        var value = element.GetString()!;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(char.IsControl)) throw new JsonException();
        return value;
    }
    private static void ValidateUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException();
                ValidateUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) ValidateUniqueProperties(item);
    }
}
