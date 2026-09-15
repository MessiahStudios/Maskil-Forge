using System.Buffers.Binary;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class Vst3BinaryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"maskil-binary-{Guid.NewGuid():N}");
    public Vst3BinaryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private string Write(string relative, byte[] bytes)
    {
        var path = Path.Combine(_root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes); return path;
    }
    private static BinaryHeaderInfo Read(byte[] bytes) => Vst3BinaryHeaders.Read(new MemoryStream(bytes));
    private static void U16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void U32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void Be32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), value);
    private static byte[] Pe(ushort machine = 0x8664)
    {
        var bytes = new byte[512]; bytes[0] = 77; bytes[1] = 90; U32(bytes, 60, 64);
        bytes[64] = 80; bytes[65] = 69; U16(bytes, 68, machine); U16(bytes, 84, 240); U16(bytes, 86, 0x2000);
        U16(bytes, 88, (ushort)(machine == 0x014c ? 0x10b : 0x20b)); return bytes;
    }
    private static byte[] Elf(ushort machine = 62, bool bigEndian = false)
    {
        var bytes = new byte[64]; bytes[0] = 0x7f; bytes[1] = 69; bytes[2] = 76; bytes[3] = 70;
        bytes[4] = 2; bytes[5] = (byte)(bigEndian ? 2 : 1); bytes[6] = 1;
        if (bigEndian)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(16), 3);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(18), machine);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(52), 64);
        }
        else { U16(bytes, 16, 3); U16(bytes, 18, machine); U16(bytes, 52, 64); }
        return bytes;
    }
    private static byte[] Mach(uint cpu = 0x0100000c)
    {
        var bytes = new byte[64]; U32(bytes, 0, 0xfeedfacf); U32(bytes, 4, cpu); U32(bytes, 12, 8); return bytes;
    }
    private static byte[] Fat(bool fat64 = false)
    {
        var bytes = new byte[512]; Be32(bytes, 0, fat64 ? 0xcafebabfu : 0xcafebabeu); Be32(bytes, 4, 2);
        var size = fat64 ? 32 : 20;
        for (var i = 0; i < 2; i++)
        {
            var cpu = i == 0 ? 0x01000007u : 0x0100000cu;
            var entry = 8 + i * size; Be32(bytes, entry, cpu);
            if (fat64)
            {
                BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(entry + 8), (ulong)(256 + i * 64));
                BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(entry + 16), 64);
            }
            else { Be32(bytes, entry + 8, (uint)(256 + i * 64)); Be32(bytes, entry + 12, 64); }
            Mach(cpu).CopyTo(bytes, 256 + i * 64);
        }
        return bytes;
    }

    [Theory]
    [InlineData(0x8664, "X64")]
    [InlineData(0x014c, "X86")]
    [InlineData(0xaa64, "Arm64")]
    [InlineData(0xa641, "Arm64EC")]
    [InlineData(0xa64e, "Arm64X")]
    public void PeLibraries_ReportHeaderMachine(int machine, string architecture)
    {
        var result = Read(Pe((ushort)machine)); Assert.Equal("Recognized", result.Status); Assert.Equal("PE", result.Format); Assert.Equal(architecture, Assert.Single(result.Architectures));
    }
    [Theory]
    [InlineData("ELF", "X64")]
    [InlineData("Mach-O", "Arm64")]
    public void OtherLibraryHeaders_ReportTheirOwnFormat(string format, string architecture)
    {
        var result = Read(format == "ELF" ? Elf() : Mach()); Assert.Equal("Recognized", result.Status); Assert.Equal(format, result.Format); Assert.Equal(architecture, Assert.Single(result.Architectures));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UniversalMach_ValidatesEachSliceBeforeReportingBothArchitectures(bool fat64)
    {
        var result = Read(Fat(fat64)); Assert.Equal("Recognized", result.Status); Assert.Equal(new[] { "X64", "Arm64" }, result.Architectures);
        Assert.Equal("Match", Vst3BinaryPreflight.Match(result, "macOS", "Arm64"));
        Assert.Equal("Match", Vst3BinaryPreflight.Match(result, "macOS", "X64"));
    }
    [Theory]
    [InlineData("truncated")]
    [InlineData("pe-offset")]
    [InlineData("pe-signature")]
    [InlineData("pe-not-dll")]
    [InlineData("pe-bitness")]
    [InlineData("elf-bitness")]
    [InlineData("elf-not-library")]
    [InlineData("fat-offset")]
    [InlineData("fat-overlap")]
    [InlineData("fat-cpu-disagreement")]
    [InlineData("fat-count")]
    public void MalformedHeaders_NeverProduceAMatch(string scenario)
    {
        var bytes = scenario.StartsWith("fat") ? Fat() : scenario.StartsWith("elf") ? Elf() : Pe();
        switch (scenario)
        {
            case "truncated": bytes = bytes[..20]; break;
            case "pe-offset": U32(bytes, 60, uint.MaxValue); break;
            case "pe-signature": bytes[65] = 0; break;
            case "pe-not-dll": U16(bytes, 86, 0); break;
            case "pe-bitness": U16(bytes, 88, 0x10b); break;
            case "elf-bitness": bytes[4] = 1; break;
            case "elf-not-library": U16(bytes, 16, 2); break;
            case "fat-offset": Be32(bytes, 16, uint.MaxValue); break;
            case "fat-overlap": Be32(bytes, 36, 256); break;
            case "fat-cpu-disagreement": Be32(bytes, 8, 0x0100000c); break;
            case "fat-count": Be32(bytes, 4, 1000); break;
        }
        var result = Read(bytes); Assert.NotEqual("Recognized", result.Status); Assert.Empty(result.Architectures); Assert.Equal("Unknown", Vst3BinaryPreflight.Match(result, "Windows", "X64"));
    }
    [Fact]
    public void Match_IsConservativeAboutFormatsCpuEndiannessAndHybridWindows()
    {
        Assert.Equal("Different", Vst3BinaryPreflight.Match(Read(Pe()), "macOS", "X64"));
        Assert.Equal("Different", Vst3BinaryPreflight.Match(Read(Pe(0x014c)), "Windows", "X64"));
        Assert.Equal("Unknown", Vst3BinaryPreflight.Match(Read(Pe(0xa641)), "Windows", "X64"));
        Assert.Equal("Unknown", Vst3BinaryPreflight.Match(Read(Pe(0xa64e)), "Windows", "Arm64"));
        Assert.Equal("Different", Vst3BinaryPreflight.Match(Read(Elf(bigEndian: true)), "Linux", "X64"));
    }
    [Fact]
    public void ConventionalPaths_ReadActualHeadersInsteadOfTrustingFolderNames()
    {
        var path = Write("Fixture.vst3/Contents/x86_64-win/Fixture.vst3", Pe(0x014c));
        var before = File.ReadAllBytes(path);
        var result = new Vst3BinaryPreflight("Windows", "X64").Inspect(Path.Combine(_root, "Fixture.vst3"), true);
        var file = Assert.Single(result.Files); Assert.Equal("Different", file.HostMatch); Assert.Equal("X86", Assert.Single(file.Architectures));
        Assert.Equal("Contents/x86_64-win/Fixture.vst3", file.Source); Assert.Equal(before, File.ReadAllBytes(path));
    }
    [Fact]
    public async Task DiscoveryKeepsInvalidBinariesAndRefreshesChangedHeaders()
    {
        var path = Write("Flat.vst3", [1, 2, 3]);
        var scanner = new Vst3Discovery("Windows", [new("Fixture", "fixture", _root)]);
        var first = Assert.Single(Assert.Single((await scanner.ScanAsync()).Locations).Candidates);
        Assert.Equal("Invalid", Assert.Single(first.Binary.Files).Status);
        File.WriteAllBytes(path, Pe());
        var next = Assert.Single(Assert.Single((await scanner.ScanAsync()).Locations).Candidates);
        Assert.Equal("PE", Assert.Single(next.Binary.Files).Format);
    }
    [Fact]
    public void MissingPathsBudgetAndCancellation_AreExplicit()
    {
        var path = Path.Combine(_root, "Empty.vst3"); Directory.CreateDirectory(path);
        var reader = new Vst3BinaryPreflight("Windows", "X64", 1);
        Assert.Equal("NoConventionalBinary", reader.Inspect(path, true).Status);
        Assert.Equal("ScanLimit", reader.Inspect(path, true).Status);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => reader.Inspect(path, true, canceled.Token));
    }
    [Fact]
    public void LinkedBinary_IsSkipped()
    {
        if (OperatingSystem.IsWindows()) return;
        var target = Write("target", Pe());
        var bundle = Path.Combine(_root, "Fixture.vst3"); var folder = Path.Combine(bundle, "Contents/x86_64-win"); Directory.CreateDirectory(folder);
        File.CreateSymbolicLink(Path.Combine(folder, "Fixture.vst3"), target);
        var result = new Vst3BinaryPreflight("Windows", "X64").Inspect(bundle, true);
        Assert.Equal("LinkedPath", Assert.Single(result.Files).Status);
    }
}
