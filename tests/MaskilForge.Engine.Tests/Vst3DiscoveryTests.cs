using System.Text.Json;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class Vst3DiscoveryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"maskil-vst3-{Guid.NewGuid():N}");
    public Vst3DiscoveryTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);
    private string Folder(string path) { var full = Path.Combine(_directory, path); Directory.CreateDirectory(full); return full; }
    private string FileAt(string path)
    {
        var full = Path.Combine(_directory, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "Not executable plugin data. Discovery must not load this file.");
        return full;
    }
    private Vst3Discovery Scanner(Vst3ScanLimits? limits = null) => new("Test", [new("Fixture", "Test folder", _directory)], limits);

    [Fact]
    public async Task Discovery_ListsFilesAndBundlesWithoutReadingTheirContents()
    {
        FileAt("Vendor/声.VST3");
        FileAt("Vendor/Synth.vst3/Contents/inside.vst3");
        FileAt("ignored.dll");
        FileAt("ignored.vst");
        var scan = await Scanner().ScanAsync();
        var location = Assert.Single(scan.Locations);
        Assert.Equal("Complete", location.Status);
        Assert.Equal(2, location.Candidates.Count);
        Assert.Contains(location.Candidates, candidate => candidate is { Name: "声.VST3", RelativePath: "Vendor/声.VST3", Kind: "File" });
        Assert.Contains(location.Candidates, candidate => candidate is { Name: "Synth.vst3", RelativePath: "Vendor/Synth.vst3", Kind: "Bundle" });
        Assert.DoesNotContain(_directory, JsonSerializer.Serialize(scan));
        Assert.Equal("Not executable plugin data. Discovery must not load this file.", File.ReadAllText(Path.Combine(_directory, "Vendor/声.VST3")));
    }

    [Fact]
    public async Task CopiesWithTheSameName_RemainDistinctAndSorted()
    {
        FileAt("z/Synth.vst3"); FileAt("a/Synth.vst3");
        var candidates = Assert.Single((await Scanner().ScanAsync()).Locations).Candidates;
        Assert.Equal(new[] { "a/Synth.vst3", "z/Synth.vst3" }, candidates.Select(item => item.RelativePath));
    }

    [Fact]
    public async Task EachScanReflectsCurrentFiles_WithoutPersistingAnInventory()
    {
        var path = FileAt("Old.vst3");
        var scanner = Scanner();
        Assert.Single(Assert.Single((await scanner.ScanAsync()).Locations).Candidates);
        File.Delete(path);
        FileAt("New.vst3");
        Assert.Equal("New.vst3", Assert.Single(Assert.Single((await scanner.ScanAsync()).Locations).Candidates).Name);
        Assert.Single(Directory.EnumerateFileSystemEntries(_directory));
    }

    [Fact]
    public async Task MissingAndInvalidRoots_AreReportedWithoutHidingOtherResults()
    {
        var file = FileAt("not-a-folder");
        var empty = Folder("empty");
        var scanner = new Vst3Discovery("Test", [new("Missing", "missing", Path.Combine(_directory, "missing")), new("Invalid", "invalid", file), new("Empty", "empty", empty)]);
        var scan = await scanner.ScanAsync();
        Assert.Equal(new[] { "Missing", "Unavailable", "Complete" }, scan.Locations.Select(item => item.Status));
        Assert.All(scan.Locations, item => Assert.Empty(item.Candidates));
    }

    [Theory]
    [InlineData("CandidateLimit")]
    [InlineData("EntryLimit")]
    [InlineData("DepthLimit")]
    public async Task BoundedScans_ReportPartialResults(string issue)
    {
        var limits = issue switch
        {
            "CandidateLimit" => new Vst3ScanLimits(MaxCandidates: 1),
            "EntryLimit" => new Vst3ScanLimits(MaxEntries: 1),
            _ => new Vst3ScanLimits(MaxDepth: 0)
        };
        FileAt("First.vst3"); FileAt("Second.vst3"); FileAt("Nested/Third.vst3");
        var location = Assert.Single((await Scanner(limits).ScanAsync()).Locations);
        Assert.Equal("Partial", location.Status);
        Assert.Contains(issue, location.Issues);
        if (issue == "CandidateLimit") Assert.Single(location.Candidates);
        if (issue == "DepthLimit") Assert.DoesNotContain(location.Candidates, item => item.Name == "Third.vst3");
    }

    [Fact]
    public async Task CanceledRequest_DoesNotPreventTheNextScan()
    {
        var scanner = Scanner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.ScanAsync(cancellation.Token));
        Assert.Equal("Complete", Assert.Single((await scanner.ScanAsync()).Locations).Status);
    }

    [Fact]
    public async Task SymbolicLinks_AreSkippedAtTheRootAndInsideFolders()
    {
        // Windows symlink creation requires an OS privilege; Linux CI exercises this filesystem case.
        if (OperatingSystem.IsWindows()) return;
        var target = Folder("target");
        FileAt("target/External.vst3");
        var root = Folder("scan");
        Directory.CreateSymbolicLink(Path.Combine(root, "linked"), target);
        Directory.CreateSymbolicLink(Path.Combine(_directory, "root-link"), target);
        File.CreateSymbolicLink(Path.Combine(root, "Linked.vst3"), Path.Combine(target, "External.vst3"));
        var scanner = new Vst3Discovery("Test", [new("Root link", "link", Path.Combine(_directory, "root-link")), new("Root", "root", root)]);
        var scan = await scanner.ScanAsync();
        Assert.Equal("SkippedLink", scan.Locations[0].Status);
        Assert.Equal("Partial", scan.Locations[1].Status);
        Assert.Contains("LinkedEntry", scan.Locations[1].Issues);
        Assert.All(scan.Locations, item => Assert.Empty(item.Candidates));
    }

    [Theory]
    [InlineData("Windows", 4, "Programs/Common/VST3")]
    [InlineData("macOS", 3, "Library/Audio/Plug-Ins/VST3")]
    [InlineData("Linux", 6, ".vst3")]
    public void PlatformLocations_PreferTheUserFolderAndKeepHostPathsSeparate(string platform, int count, string userSuffix)
    {
        var host = new Vst3HostPaths(platform, Path.Combine(_directory, "home"), Path.Combine(_directory, "local"),
            Path.Combine(_directory, "common"), Path.Combine(_directory, "common-x86"), Path.Combine(_directory, "app"));
        var locations = Vst3SearchLocations.Resolve(host);
        Assert.Equal(count, locations.Count);
        Assert.Equal("Current user", locations[0].Name);
        Assert.EndsWith(userSuffix, locations[0].Path.Replace('\\', '/'));
        Assert.Equal("Maskil host", locations[^1].Name);
        Assert.DoesNotContain(locations, item => item.Path.Contains("/Network/"));
    }

    [Fact]
    public void MissingSpecialFolders_DoNotBecomeRelativePaths()
    {
        Assert.Empty(Vst3SearchLocations.Resolve(new("Windows", "", "", "", "", "")));
        Assert.Empty(Vst3SearchLocations.Resolve(new("Unsupported", _directory, _directory, _directory, _directory, _directory)));
        var locations = Vst3SearchLocations.Resolve(new("Windows", "", "", _directory, _directory, ""));
        Assert.Single(locations);
    }
}
