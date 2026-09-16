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
}
