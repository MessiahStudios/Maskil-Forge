using System.Diagnostics;
using System.Net;
using MaskilForge.Api;
using MaskilForge.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace MaskilForge.Engine.Tests;

public sealed class Vst3NativeCheckTests : IDisposable
{
    // /private/tmp avoids macOS's /var symlink when comparing CoreFoundation's executable path.
    private readonly string _root = Path.Combine(OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath(), $"maskil-native-{Guid.NewGuid():N}");
    private static string Worker => Path.Combine(AppContext.BaseDirectory, "native/maskil-vst3-probe");
    public Vst3NativeCheckTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private async Task<(string Bundle, string Binary)> Fixture(int mode)
    {
        var bundle = Path.Combine(_root, $"Mode{mode}.vst3");
        var binary = Path.Combine(bundle, "Contents/MacOS/Fixture");
        Directory.CreateDirectory(Path.GetDirectoryName(binary)!);
        await File.WriteAllTextAsync(Path.Combine(bundle, "Contents/Info.plist"), $"""
            <?xml version="1.0"?><plist version="1.0"><dict>
            <key>CFBundleExecutable</key><string>Fixture</string>
            <key>CFBundleIdentifier</key><string>org.maskil.fixture{mode}</string>
            <key>CFBundlePackageType</key><string>BNDL</string>
            </dict></plist>
            """);
        var start = new ProcessStartInfo("/usr/bin/clang") { UseShellExecute = false, RedirectStandardError = true };
        foreach (var argument in new[] { "-bundle", "-DMODE=" + mode, Path.Combine(AppContext.BaseDirectory, "NativeFixtures/Module.c"), "-o", binary })
            start.ArgumentList.Add(argument);
        using var compiler = Process.Start(start)!;
        var errors = await compiler.StandardError.ReadToEndAsync();
        await compiler.WaitForExitAsync();
        Assert.True(compiler.ExitCode == 0, errors);
        return (bundle, binary);
    }

    [Theory]
    [InlineData(0, "Completed", "ModuleUnloaded")]
    [InlineData(1, "MissingEntryPoints", "ModuleLoaded")]
    [InlineData(2, "EntryRejected", "EntryPointsResolved")]
    [InlineData(3, "ExitRejected", "ModuleEntered")]
    [InlineData(4, "TimedOut", "EntryPointsResolved")]
    [InlineData(5, "WorkerCrashed", "EntryPointsResolved")]
    [InlineData(6, "TimedOut", "BundleOpened")]
    [InlineData(7, "OutputLimit", "EntryPointsResolved")]
    [InlineData(8, "TimedOut", "ModuleEntered")]
    [InlineData(9, "WorkerCrashed", "ModuleExited")]
    public async Task NativeLifecycle_ContainsFailuresAndReportsLastCompletedStage(int mode, string status, string stage)
    {
        if (!OperatingSystem.IsMacOS()) return;
        Assert.True(File.Exists(Worker), "Build must produce and copy the macOS worker.");
        var fixture = await Fixture(mode);
        var runner = new Vst3ProbeProcess(TimeSpan.FromSeconds(1));
        var result = await runner.RunAsync(Worker, fixture.Bundle, fixture.Binary);
        Assert.Equal(status, result.Status);
        Assert.Equal(stage, result.LastCompletedStage);
        // A fresh process still works after a crash, timeout, or output flood.
        var healthy = await Fixture(0);
        Assert.Equal("Completed", (await runner.RunAsync(Worker, healthy.Bundle, healthy.Binary)).Status);
    }

    [Fact]
    public async Task Cancellation_StopsAnActiveWorker()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var fixture = await Fixture(4);
        using var canceled = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await new Vst3ProbeProcess().RunAsync(Worker, fixture.Bundle, fixture.Binary, canceled.Token);
        Assert.Equal("Cancelled", result.Status);
        Assert.Equal("EntryPointsResolved", result.LastCompletedStage);
    }

    [Fact]
    public async Task DiscoveryNeverLoadsCode_AndNativeCheckRequiresAnExactFreshCandidate()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var fixture = await Fixture(0);
        var scanner = new Vst3Discovery("macOS", [new("Fixture", "fixture", _root)]);
        var candidate = Assert.Single(Assert.Single((await scanner.ScanAsync()).Locations).Candidates);
        var check = new Vst3NativeCheck(scanner);
        var request = new Vst3NativeCheckRequest("Fixture", candidate.RelativePath, candidate.Binary.MacExecutable!.Sha256!);
        Assert.Equal("CandidateUnavailable", (await check.CheckAsync(request with { RelativePath = fixture.Bundle })).Status);
        Assert.Equal("CandidateUnavailable", (await check.CheckAsync(request with { RelativePath = "../Mode0.vst3" })).Status);
        Assert.Equal("CandidateUnavailable", (await check.CheckAsync(request with { Location = "Other" })).Status);
        Assert.Equal("RescanRequired", (await check.CheckAsync(request with { ExpectedPlistSha256 = "stale" })).Status);
        var result = await check.CheckAsync(request);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("ModuleUnloaded", result.LastCompletedStage);
        Assert.Equal("Contents/MacOS/Fixture", result.BinarySource);
        Assert.Equal(64, result.BinarySha256!.Length);
        Assert.Equal(request.ExpectedPlistSha256, result.PlistSha256);
        File.Delete(fixture.Binary);
        Assert.Equal("HeaderNotMatched", (await check.CheckAsync(request)).Status);
    }

    [Fact]
    public async Task LinkedCandidate_IsNotAcceptedByTheNativeResolver()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var fixture = await Fixture(0);
        var scanner = new Vst3Discovery("macOS", [new("Fixture", "fixture", _root)]);
        var destination = Path.Combine(_root, "Moved.vst3");
        Directory.Move(fixture.Bundle, destination);
        Directory.CreateSymbolicLink(fixture.Bundle, destination);
        Assert.Null(await scanner.ResolveNativeCandidateAsync("Fixture", "Mode0.vst3", default));
    }

    [Fact]
    public async Task OverlappingChecks_AreRejected_AndCancellationReleasesTheGate()
    {
        if (!OperatingSystem.IsMacOS()) return;
        await Fixture(4);
        await Fixture(0);
        var scanner = new Vst3Discovery("macOS", [new("Fixture", "fixture", _root)]);
        var candidates = (await scanner.ScanAsync()).Locations.Single().Candidates;
        Vst3NativeCheckRequest Request(string path) => new("Fixture", path,
            candidates.Single(item => item.RelativePath == path).Binary.MacExecutable!.Sha256!);
        var check = new Vst3NativeCheck(scanner);
        using var cancel = new CancellationTokenSource();
        var first = check.CheckAsync(Request("Mode4.vst3"), cancel.Token);
        Assert.Equal("Busy", (await check.CheckAsync(Request("Mode0.vst3"))).Status);
        cancel.Cancel();
        try { Assert.Equal("Cancelled", (await first).Status); }
        catch (OperationCanceledException) { /* Cancellation can arrive during the preflight scan. */ }
        Assert.Equal("Completed", (await check.CheckAsync(Request("Mode0.vst3"))).Status);
    }

    [Fact]
    public async Task FactoryEnumerationReadsRuntimeClassesWithoutCreatingComponents()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var fixture = await Fixture(10);
        var scanner = new Vst3Discovery("macOS", [new("Fixture", "fixture", _root)]);
        var candidate = Assert.Single((await scanner.ScanAsync()).Locations.Single().Candidates);
        var request = new Vst3NativeCheckRequest("Fixture", candidate.RelativePath, candidate.Binary.MacExecutable!.Sha256!);
        var result = await new Vst3NativeCheck(scanner).EnumerateFactoryAsync(request);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("ModuleUnloaded", result.LastCompletedStage);
        Assert.Equal(["Fixture 1", "Fixture 2"], result.Classes.Select(item => item.Name));
        Assert.All(result.Classes, item => Assert.Equal("Audio Module Class", item.Category));
    }

    [Fact]
    public async Task MissingWorker_ReturnsAnExplicitResult()
    {
        var result = await new Vst3ProbeProcess().RunAsync(Path.Combine(_root, "absent-worker"), "bundle", "binary");
        Assert.Equal("WorkerUnavailable", result.Status);
        Assert.Equal("NotStarted", result.LastCompletedStage);
    }

    [Theory]
    [InlineData("127.0.0.1", "localhost:5072", "http://localhost:5072", true)]
    [InlineData("::1", "localhost:5072", "http://localhost:5173", true)]
    [InlineData("::ffff:127.0.0.1", "127.0.0.1:5072", "", true)]
    [InlineData("192.168.1.7", "localhost:5072", "http://localhost:5072", false)]
    [InlineData("127.0.0.1", "attacker.example:5072", "http://attacker.example:5072", false)]
    [InlineData("127.0.0.1", "localhost:5072", "https://attacker.example", false)]
    [InlineData("127.0.0.1", "localhost:5072", "null", false)]
    public void NativeEndpoint_RequiresLocalHostAndTrustedOrigin(string address, string host, string origin, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        context.Request.Host = new HostString(host);
        context.Request.Scheme = "http";
        if (origin.Length > 0) context.Request.Headers.Origin = origin;
        Assert.Equal(expected, NativePluginAccess.IsLocalRequest(context));
    }
}
