using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class Vst3MacBundleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"maskil-plist-{Guid.NewGuid():N}");
    private string Bundle => Path.Combine(_root, "Display Name.vst3");
    public Vst3MacBundleTests() => Directory.CreateDirectory(Bundle);
    public void Dispose() => Directory.Delete(_root, true);
    private string Write(string relative, byte[] bytes)
    {
        var path = Path.Combine(Bundle, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }
    private string Plist(string entries) => Write("Contents/Info.plist", Encoding.UTF8.GetBytes(
        $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist version=\"1.0\"><dict>{entries}</dict></plist>"));
    private void Declare(string name) => Plist($"<key>CFBundleExecutable</key><string>{name}</string>");
    private static byte[] Mach(uint cpu = 0x0100000c)
    {
        var bytes = new byte[64];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0xfeedfacf);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), cpu);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 8);
        return bytes;
    }

    [Fact]
    public void DeclaredName_ReplacesBundleNameGuess_AndAttributesExactPlistBytes()
    {
        Declare("Actual_夜");
        Write("Contents/MacOS/Actual_夜", Mach());
        Write("Contents/MacOS/Display Name", Mach(0x01000007));
        var plist = File.ReadAllBytes(Path.Combine(Bundle, "Contents/Info.plist"));
        var result = new Vst3BinaryPreflight("macOS", "Arm64").Inspect(Bundle, true);
        var file = Assert.Single(result.Files);
        Assert.Equal("Contents/MacOS/Actual_夜", file.Source);
        Assert.Equal("Match", file.HostMatch);
        Assert.Equal("Available", result.MacExecutable!.Status);
        Assert.Equal("Contents/Info.plist", result.MacExecutable.Source);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(plist)), result.MacExecutable.Sha256);
        Assert.Equal(plist, File.ReadAllBytes(Path.Combine(Bundle, "Contents/Info.plist")));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute")]
    [InlineData("sub/file")]
    [InlineData("sub\\file")]
    [InlineData("C:outside")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad&#10;name")]
    public void DeclaredPathsAndControlCharacters_AreNeverResolved(string name)
    {
        Declare(name);
        var result = new Vst3BinaryPreflight("macOS", "Arm64").Inspect(Bundle, true);
        Assert.Equal("InvalidExecutableName", result.MacExecutable!.Status);
        Assert.Null(result.MacExecutable.Executable);
        Assert.Empty(result.Files);
    }

    [Theory]
    [InlineData("<key>CFBundleExecutable</key><integer>42</integer>")]
    [InlineData("<key>CFBundleExecutable</key><string>A</string><key>CFBundleExecutable</key><string>B</string>")]
    [InlineData("<key>CFBundleExecutable</key><string><string>A</string></string>")]
    [InlineData("<key>CFBundleExecutable</key>")]
    [InlineData("<string>CFBundleExecutable</string><string>A</string>")]
    public void InvalidTopLevelDeclarations_DoNotSupplyAFilename(string entries)
    {
        Plist(entries);
        Assert.Equal("InvalidOrUnsupported", Vst3MacBundle.Read(Bundle).Status);
    }

    [Fact]
    public void MissingOrUnsupportedDeclaration_KeepsConventionalHeaderEvidenceSeparate()
    {
        Write("Contents/MacOS/Display Name", Mach());
        var first = new Vst3BinaryPreflight("macOS", "Arm64").Inspect(Bundle, true);
        Assert.Equal("Missing", first.MacExecutable!.Status);
        Assert.Equal("Match", Assert.Single(first.Files).HostMatch);
        Write("Contents/Info.plist", "bplist00fixture"u8.ToArray());
        var next = new Vst3BinaryPreflight("macOS", "Arm64").Inspect(Bundle, true);
        Assert.Equal("UnsupportedFormat", next.MacExecutable!.Status);
        Assert.Equal("Contents/MacOS/Display Name", Assert.Single(next.Files).Source);
        Plist("<key>Nested</key><dict><key>CFBundleExecutable</key><string>Other</string></dict>");
        Assert.Equal("MissingExecutable", Vst3MacBundle.Read(Bundle).Status);
    }

    [Fact]
    public void DeclaredButAbsentBinary_IsExplicit_AndDoesNotFallBackToADecoy()
    {
        Declare("Absent");
        Write("Contents/MacOS/Display Name", Mach());
        var file = Assert.Single(new Vst3BinaryPreflight("macOS", "Arm64").Inspect(Bundle, true).Files);
        Assert.Equal("Contents/MacOS/Absent", file.Source);
        Assert.Equal("Missing", file.Status);
        Assert.Equal("Unknown", file.HostMatch);
    }

    [Theory]
    [InlineData("Info.plist")]
    [InlineData("MacOS")]
    [InlineData("Executable")]
    [InlineData("Contents")]
    public void LinkedPlistOrDeclaredBinaryPath_IsSkipped(string linkedPart)
    {
        if (OperatingSystem.IsWindows()) return;
        Declare("Actual");
        var executable = Write("Contents/MacOS/Actual", Mach());
        var path = linkedPart switch
        {
            "Info.plist" => Path.Combine(Bundle, "Contents/Info.plist"),
            "MacOS" => Path.Combine(Bundle, "Contents/MacOS"),
            "Contents" => Path.Combine(Bundle, "Contents"),
            _ => executable
        };
        var target = Path.Combine(_root, "target");
        if (Directory.Exists(path)) { Directory.Move(path, target); Directory.CreateSymbolicLink(path, target); }
        else { File.Move(path, target); File.CreateSymbolicLink(path, target); }
        var result = new Vst3BinaryPreflight("macOS", "Arm64").Inspect(Bundle, true);
        if (linkedPart is "Info.plist" or "Contents") Assert.Equal("LinkedPath", result.MacExecutable!.Status);
        else Assert.Equal("LinkedPath", Assert.Single(result.Files).Status);
        Assert.DoesNotContain(result.Files, file => file.HostMatch == "Match");
    }

    [Fact]
    public void FileSizeDepthNameLengthAndCancellation_AreBounded()
    {
        Write("Contents/Info.plist", new byte[Vst3MacBundle.MaxFileBytes + 1]);
        Assert.Equal("TooLarge", Vst3MacBundle.Read(Bundle).Status);
        Plist("<key>Deep</key>" + string.Concat(Enumerable.Repeat("<array>", 20)) + string.Concat(Enumerable.Repeat("</array>", 20)));
        Assert.Equal("InvalidOrUnsupported", Vst3MacBundle.Read(Bundle).Status);
        Declare(new string('a', 256));
        Assert.Equal("InvalidExecutableName", Vst3MacBundle.Read(Bundle).Status);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => Vst3MacBundle.Read(Bundle, canceled.Token));
        Assert.Null(new Vst3BinaryPreflight("macOS", "Arm64", 0).Inspect(Bundle, true).MacExecutable);
    }

    [Fact]
    public void XmlEntities_AreNotExpanded()
    {
        Write("Contents/Info.plist", Encoding.UTF8.GetBytes("<!DOCTYPE plist [<!ENTITY name SYSTEM 'file:///etc/passwd'>]><plist><dict><key>CFBundleExecutable</key><string>&name;</string></dict></plist>"));
        Assert.Equal("InvalidOrUnsupported", Vst3MacBundle.Read(Bundle).Status);
    }

    [Fact]
    public async Task Rescanning_RefreshesDeclarationDigestAndActualHeaderEvidence()
    {
        Declare("First");
        Write("Contents/MacOS/First", Mach());
        Write("Contents/MacOS/Second", [1, 2, 3]);
        var scanner = new Vst3Discovery("macOS", [new("Fixture", "fixture", _root)]);
        var first = Assert.Single(Assert.Single((await scanner.ScanAsync()).Locations).Candidates).Binary;
        Declare("Second");
        var second = Assert.Single(Assert.Single((await scanner.ScanAsync()).Locations).Candidates).Binary;
        Assert.NotEqual(first.MacExecutable!.Sha256, second.MacExecutable!.Sha256);
        Assert.Equal("Invalid", Assert.Single(second.Files).Status);
        Assert.Equal("Unknown", Assert.Single(second.Files).HostMatch);
        Assert.Equal("Contents/MacOS/Second", Assert.Single(second.Files).Source);
    }
}
