using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class Vst3MetadataTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"maskil-metadata-{Guid.NewGuid():N}");
    private const string Current = "Contents/Resources/moduleinfo.json";
    private const string Legacy = "Contents/moduleinfo.json";
    private const string Manifest = """
        {
          "Name": "声 Module", "Version": "2.1", "Factory Info": { "Vendor": "Fixture Vendor", "URL": "https://invalid.example/never-fetch" },
          "Classes": [
            { "CID": "0123456789abcdef0123456789abcdef", "Name": "Fixture EQ", "Category": "Audio Module Class", "Vendor": "Class Vendor", "Version": "1.2", "SDKVersion": "VST 3.7.8", "Sub Categories": ["Fx", "EQ"] },
            { "CID": "1123456789abcdef0123456789abcdef", "Name": "Fixture Controller", "Category": "Component Controller Class" }
          ]
        }
        """;
    public Vst3MetadataTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);
    private string Bundle(string name = "Fixture.vst3") { var path = Path.Combine(_root, name); Directory.CreateDirectory(path); return path; }
    private static string Write(string bundle, string content, string source = Current)
    {
        var path = Path.Combine(bundle, source);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    [Fact]
    public void ReportsModuleAndClassesWithProvenance_WithoutInferringMissingFields()
    {
        var bundle = Bundle();
        var path = Write(bundle, Manifest);
        var before = File.ReadAllBytes(path);
        var metadata = new Vst3MetadataReader().Read(bundle);
        Assert.Equal("Available", metadata.Status);
        Assert.Equal(Current, metadata.Source);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(before)), metadata.Sha256);
        Assert.Equal("声 Module", metadata.Module!.Name);
        Assert.Equal("Fixture Vendor", metadata.Module.Vendor);
        Assert.Equal("2.1", metadata.Module.Version);
        Assert.Equal(2, metadata.Module.Classes.Count);
        Assert.Equal("0123456789ABCDEF0123456789ABCDEF", metadata.Module.Classes[0].Id);
        Assert.Equal(new[] { "Fx", "EQ" }, metadata.Module.Classes[0].SubCategories);
        Assert.Equal("Class Vendor", metadata.Module.Classes[0].Vendor);
        Assert.Null(metadata.Module.Classes[1].Vendor);
        Assert.Null(metadata.Module.Classes[1].Version);
        Assert.DoesNotContain(_root, JsonSerializer.Serialize(metadata));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void EmptyOptionalClassFields_AreNotInferredFromTheFactory()
    {
        var bundle = Bundle();
        Write(bundle, Manifest.Replace("\"Class Vendor\"", "\"\"").Replace("\"1.2\"", "\"\"").Replace("\"VST 3.7.8\"", "\"\""));
        var result = new Vst3MetadataReader().Read(bundle);
        Assert.Equal("Available", result.Status);
        Assert.Null(result.Module!.Classes[0].Vendor);
        Assert.Null(result.Module.Classes[0].Version);
        Assert.Null(result.Module.Classes[0].SdkVersion);
    }

    [Fact]
    public void CommentsTrailingCommasAndUtf8Bom_AreAcceptedAndDigestCoversOriginalBytes()
    {
        var bundle = Bundle();
        var path = Write(bundle, Manifest.Replace("\"Version\": \"2.1\"", "/* comment */ \"Version\": \"2.1\"").Replace("\"EQ\"]", "\"EQ\",]"));
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(File.ReadAllBytes(path)).ToArray();
        File.WriteAllBytes(path, bytes);
        var metadata = new Vst3MetadataReader().Read(bundle);
        Assert.Equal("Available", metadata.Status);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), metadata.Sha256);
    }

    [Fact]
    public void CurrentLocationWins_AndLegacyIsUsedOnlyWhenCurrentIsMissing()
    {
        var bundle = Bundle();
        Write(bundle, Manifest, Legacy);
        Assert.Equal(Legacy, new Vst3MetadataReader().Read(bundle).Source);
        var currentPath = Write(bundle, Manifest.Replace("声 Module", "Current Module"));
        Assert.Equal("Current Module", new Vst3MetadataReader().Read(bundle).Module!.Name);
        File.WriteAllText(currentPath, "invalid current metadata");
        var invalid = new Vst3MetadataReader().Read(bundle);
        Assert.Equal("InvalidOrUnsupported", invalid.Status);
        Assert.Equal(Current, invalid.Source);
        Assert.Null(invalid.Module);
    }

    [Theory]
    [InlineData("missing-name")]
    [InlineData("wrong-type")]
    [InlineData("empty-vendor")]
    [InlineData("invalid-id")]
    [InlineData("duplicate-id")]
    [InlineData("long-text")]
    [InlineData("control-text")]
    [InlineData("classes-type")]
    [InlineData("too-many-classes")]
    [InlineData("category-type")]
    [InlineData("too-many-categories")]
    public void MalformedOrExcessiveDeclarations_DoNotLeakPartialMetadata(string scenario)
    {
        var node = JsonNode.Parse(Manifest)!;
        var first = node["Classes"]![0]!;
        switch (scenario)
        {
            case "missing-name": node.AsObject().Remove("Name"); break;
            case "wrong-type": node["Version"] = 2; break;
            case "empty-vendor": node["Factory Info"]!["Vendor"] = " "; break;
            case "invalid-id": first["CID"] = "not-a-class-id"; break;
            case "duplicate-id": node["Classes"]![1]!["CID"] = first["CID"]!.GetValue<string>().ToUpperInvariant(); break;
            case "long-text": first["Name"] = new string('a', 257); break;
            case "control-text": first["Name"] = "a\nb"; break;
            case "classes-type": node["Classes"] = "not-an-array"; break;
            case "too-many-classes": node["Classes"] = new JsonArray(Enumerable.Range(0, 129).Select(_ => first.DeepClone()).ToArray()); break;
            case "category-type": first["Sub Categories"] = "Fx"; break;
            case "too-many-categories": first["Sub Categories"] = new JsonArray(Enumerable.Range(0, 17).Select(_ => JsonValue.Create("Fx") as JsonNode).ToArray()); break;
        }
        var bundle = Bundle();
        Write(bundle, node.ToJsonString());
        var metadata = new Vst3MetadataReader().Read(bundle);
        Assert.Equal("InvalidOrUnsupported", metadata.Status);
        Assert.Null(metadata.Module);
        Assert.Null(metadata.Sha256);
    }

    [Theory]
    [InlineData("duplicate-property")]
    [InlineData("json5-single-quotes")]
    [InlineData("excessive-depth")]
    [InlineData("empty")]
    [InlineData("invalid-utf8")]
    public void AmbiguousUnsupportedAndInvalidEncodings_AreReportedWithoutThrowing(string scenario)
    {
        var bundle = Bundle();
        var content = scenario switch
        {
            "duplicate-property" => Manifest.Replace("\"Version\": \"2.1\"", "\"Version\": \"1\", \"Version\": \"2.1\""),
            "json5-single-quotes" => Manifest.Replace('"', '\''),
            "excessive-depth" => new string('[', 20) + "0" + new string(']', 20),
            "empty" => "",
            _ => Manifest
        };
        var path = Write(bundle, content);
        if (scenario == "invalid-utf8") File.WriteAllBytes(path, [0xFF, 0xFE, 0x01]);
        Assert.Equal("InvalidOrUnsupported", new Vst3MetadataReader().Read(bundle).Status);
    }

    [Fact]
    public void MissingOversizedAndScanBudgetExhaustion_AreDistinct()
    {
        var bundle = Bundle();
        var reader = new Vst3MetadataReader(1);
        Assert.Equal("Missing", reader.Read(bundle).Status);
        Write(bundle, Manifest);
        Assert.Equal("Available", reader.Read(bundle).Status);
        Assert.Equal("ScanLimit", reader.Read(bundle).Status);
        Write(bundle, new string(' ', Vst3MetadataReader.MaxFileBytes + 1));
        Assert.Equal("TooLarge", new Vst3MetadataReader().Read(bundle).Status);
    }

    [Fact]
    public async Task Discovery_PreservesBadAndFileCandidates_AndRescansMetadata()
    {
        var good = Bundle("Good.vst3"); Write(good, Manifest);
        Write(Bundle("Bad.vst3"), "invalid");
        Bundle("Missing.vst3");
        File.WriteAllText(Path.Combine(_root, "File.vst3"), "inert binary fixture");
        var scanner = new Vst3Discovery("Test", [new("Fixture", "Fixture", _root)]);
        var location = Assert.Single((await scanner.ScanAsync()).Locations);
        Assert.Equal("Complete", location.Status);
        Assert.Equal(new[] { "InvalidOrUnsupported", "NotApplicable", "Available", "Missing" }, location.Candidates.Select(item => item.Metadata.Status));
        Write(good, Manifest.Replace("\"2.1\"", "\"2.2\""));
        var updated = Assert.Single((await scanner.ScanAsync()).Locations).Candidates.Single(item => item.Name == "Good.vst3");
        Assert.Equal("2.2", updated.Metadata.Module!.Version);
        Assert.NotEqual(location.Candidates.Single(item => item.Name == "Good.vst3").Metadata.Sha256, updated.Metadata.Sha256);
    }

    [Fact]
    public void Cancellation_IsPropagated()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new Vst3MetadataReader().Read(Bundle(), canceled.Token));
    }

    [Theory]
    [InlineData("Contents")]
    [InlineData("Resources")]
    [InlineData("manifest")]
    public void LinkedManifestOrParent_IsNeverReadOrReplacedWithLegacyData(string linkAt)
    {
        // Linux CI covers symlinks; Windows junctions are checked in the API/browser fixture.
        if (OperatingSystem.IsWindows()) return;
        var target = Bundle("Target");
        Write(target, Manifest);
        var bundle = Bundle();
        var contents = Path.Combine(bundle, "Contents");
        if (linkAt == "Contents") Directory.CreateSymbolicLink(contents, Path.Combine(target, "Contents"));
        else
        {
            Write(bundle, Manifest, Legacy);
            if (linkAt == "Resources") Directory.CreateSymbolicLink(Path.Combine(contents, "Resources"), Path.Combine(target, "Contents/Resources"));
            else
            {
                Directory.CreateDirectory(Path.Combine(contents, "Resources"));
                File.CreateSymbolicLink(Path.Combine(bundle, Current), Path.Combine(target, Current));
            }
        }
        var result = new Vst3MetadataReader().Read(bundle);
        Assert.Equal("LinkedPath", result.Status);
        Assert.Null(result.Module);
    }
}
