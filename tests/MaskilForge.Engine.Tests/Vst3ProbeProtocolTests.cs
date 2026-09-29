using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class Vst3ProbeProtocolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"maskil-protocol-{Guid.NewGuid():N}");
    public Vst3ProbeProtocolTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"protocolVersion\":1,\"stage\":\"ModuleUnloaded\",\"status\":\"Completed\"}")]
    [InlineData("{\"protocolVersion\":2,\"stage\":\"WorkerStarted\",\"status\":\"Progress\"}")]
    [InlineData("{\"protocolVersion\":1,\"stage\":\"WorkerStarted\",\"status\":\"Progress\"}")]
    public async Task MalformedOutOfOrderOrIncompleteOutput_IsNeverSuccess(string output)
    {
        if (OperatingSystem.IsWindows()) return;
        var worker = Path.Combine(_root, "fixture.sh");
        await File.WriteAllTextAsync(worker, "#!/bin/sh\nprintf '%s\\n' '" + output + "'\n");
        File.SetUnixFileMode(worker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var result = await new Vst3ProbeProcess().RunAsync(worker, "bundle", "binary");
        Assert.Equal("InvalidWorkerOutput", result.Status);
    }

    [Theory]
    [InlineData("{\"protocolVersion\":2,\"stage\":\"WorkerStarted\",\"status\":\"Progress\"}")]
    [InlineData("{\"protocolVersion\":1,\"stage\":\"FactoryClass\",\"status\":\"Class\",\"id\":\"00000000000000000000000000000001\",\"name\":\"Early\",\"category\":\"Audio Module Class\"}")]
    [InlineData("{\"protocolVersion\":1,\"stage\":\"WorkerStarted\",\"status\":\"Progress\"}\n{\"protocolVersion\":1,\"stage\":\"BundleOpened\",\"status\":\"Progress\"}\n{\"protocolVersion\":1,\"stage\":\"ModuleLoaded\",\"status\":\"Progress\"}\n{\"protocolVersion\":1,\"stage\":\"EntryPointsResolved\",\"status\":\"Progress\"}\n{\"protocolVersion\":1,\"stage\":\"ModuleEntered\",\"status\":\"Progress\"}\n{\"protocolVersion\":1,\"stage\":\"ModuleExited\",\"status\":\"Progress\"}\n{\"protocolVersion\":1,\"stage\":\"ModuleUnloaded\",\"status\":\"Completed\"}")]
    public async Task FactoryProtocol_RejectsWrongVersionOutOfOrderOrMissingFactoryCompletion(string output)
    {
        if (OperatingSystem.IsWindows()) return;
        var worker = Path.Combine(_root, "factory-fixture.sh");
        await File.WriteAllTextAsync(worker, "#!/bin/sh\nprintf '%s\\n' '" + output + "'\n");
        File.SetUnixFileMode(worker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var result = await new Vst3FactoryProbeProcess().RunAsync(worker, "bundle", "binary");
        Assert.Equal("InvalidWorkerOutput", result.Status);
    }

    [Fact]
    public async Task ComponentProtocol_RejectsBusBeforeComponentInitialization()
    {
        if (OperatingSystem.IsWindows()) return;
        var worker = Path.Combine(_root, "component-fixture.sh");
        await File.WriteAllTextAsync(worker, """
            #!/bin/sh
            printf '%s\n' '{"protocolVersion":1,"status":"Bus","stage":"AudioBus","direction":"Input","index":0,"name":"Input","channelCount":2,"busType":"Main","defaultActive":true,"controlVoltage":false}'
            """);
        File.SetUnixFileMode(worker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var result = await new Vst3ComponentProbeProcess().RunAsync(worker, "bundle", "binary", "00000000000000000000000000000001");
        Assert.Equal("InvalidWorkerOutput", result.Status);
    }

    [Fact]
    public async Task PreviewProtocol_AcceptsTheOrderedRenderAndMatchingWave()
    {
        if (OperatingSystem.IsWindows()) return;
        var worker = Path.Combine(_root, "preview-fixture.sh");
        var source = Path.Combine(_root, "silence.wav");
        var payload = new byte[44 + Vst3PreviewProcess.ExpectedFrames * 4];
        "RIFF"u8.CopyTo(payload);
        BitConverter.GetBytes(payload.Length - 8).CopyTo(payload, 4);
        "WAVEfmt "u8.CopyTo(payload.AsSpan(8));
        BitConverter.GetBytes(16).CopyTo(payload, 16);
        payload[20] = 1; payload[22] = 2;
        BitConverter.GetBytes(Vst3PreviewProcess.ExpectedRate).CopyTo(payload, 24);
        await File.WriteAllBytesAsync(source, payload);
        await File.WriteAllTextAsync(worker, """
            #!/bin/sh
            cp "$(dirname "$0")/silence.wav" "$5"
            printf '%s\n' '{"protocolVersion":1,"stage":"WorkerStarted","status":"Progress"}'
            printf '%s\n' '{"protocolVersion":1,"stage":"BundleOpened","status":"Progress"}'
            printf '%s\n' '{"protocolVersion":1,"stage":"ModuleLoaded","status":"Progress"}'
            printf '%s\n' '{"protocolVersion":1,"stage":"EntryPointsResolved","status":"Progress"}'
            printf '%s\n' '{"protocolVersion":1,"stage":"ModuleEntered","status":"Progress"}'
            printf '%s\n' '{"protocolVersion":1,"stage":"ComponentCreated","status":"Progress"}'
            printf '%s\n' '{"protocolVersion":1,"stage":"ComponentInitialized","status":"Progress"}'
            printf '%s\n' '{"protocolVersion":1,"stage":"ProcessorPrepared","status":"Progress"}'
            printf '%s\n' '{"protocolVersion":1,"status":"Preview","stage":"PreviewRendered","frames":44100,"sampleRate":44100,"peak":0,"channels":2,"controllerConnected":true}'
            printf '%s\n' '{"protocolVersion":1,"stage":"PreviewRendered","status":"Progress"}'
            printf '%s\n' '{"protocolVersion":1,"stage":"ComponentTerminated","status":"Progress"}'
            printf '%s\n' '{"protocolVersion":1,"stage":"ModuleExited","status":"Progress"}'
            printf '%s\n' '{"protocolVersion":1,"status":"Completed","stage":"ModuleUnloaded"}'
            """);
        File.SetUnixFileMode(worker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var wave = Path.Combine(_root, "out.wav");
        var result = await new Vst3PreviewProcess().RunAsync(worker, "bundle", "binary", "00000000000000000000000000000001", wave);
        Assert.Equal("Completed", result.Status);
        Assert.True(result.ControllerConnected);
        Assert.Equal(payload.Length, result.Wave?.Length);
    }

    [Fact]
    public async Task PreviewProtocol_RejectsARenderReportedBeforeTheProcessorIsPrepared()
    {
        if (OperatingSystem.IsWindows()) return;
        var worker = Path.Combine(_root, "early-preview.sh");
        await File.WriteAllTextAsync(worker, """
            #!/bin/sh
            printf '%s\n' '{"protocolVersion":1,"status":"Preview","stage":"PreviewRendered","frames":44100,"sampleRate":44100,"peak":0,"channels":2,"controllerConnected":false}'
            """);
        File.SetUnixFileMode(worker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var result = await new Vst3PreviewProcess().RunAsync(worker, "bundle", "binary", "00000000000000000000000000000001", Path.Combine(_root, "missing.wav"));
        Assert.Equal("InvalidWorkerOutput", result.Status);
        Assert.Null(result.Wave);
    }
}
