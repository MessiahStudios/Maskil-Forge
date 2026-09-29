using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace MaskilForge.Infrastructure;

public sealed record Vst3PreviewOutcome(string Status, string LastCompletedStage, string ClassId,
    int Frames, int SampleRate, int Peak, bool ControllerConnected, byte[]? Wave, int? ExitCode = null);

/// <summary>Supervises one VST3 processor setup and a one-second stereo render. The wave file stays on a path chosen by the host.</summary>
public sealed class Vst3PreviewProcess(TimeSpan? timeout = null)
{
    public const int ExpectedFrames = 44_100;
    public const int ExpectedRate = 44_100;
    private const int MaxOutputBytes = 32 * 1024;
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(30);

    public async Task<Vst3PreviewOutcome> RunAsync(string worker, string bundle, string binary, string classId, string wavePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(classId) || classId.Length != 32 || !classId.All(Uri.IsHexDigit))
            return new("InvalidClassId", "NotStarted", classId, 0, 0, 0, false, null);
        classId = classId.ToUpperInvariant();
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(_timeout);
        using var stopped = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        using var process = new Process { StartInfo = new(worker) {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = true, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(worker)!
        } };
        process.StartInfo.ArgumentList.Add(bundle);
        process.StartInfo.ArgumentList.Add(binary);
        process.StartInfo.ArgumentList.Add("--render-preview");
        process.StartInfo.ArgumentList.Add(classId);
        process.StartInfo.ArgumentList.Add(wavePath);
        process.StartInfo.Environment.Clear();
        foreach (var name in new[] { "HOME", "TMPDIR", "LANG" })
            if (Environment.GetEnvironmentVariable(name) is { } value) process.StartInfo.Environment[name] = value;
        process.StartInfo.Environment["PATH"] = "/usr/bin:/bin";
        var parser = new Parser(classId);
        var overflow = false;
        try { if (!process.Start()) return new("WorkerUnavailable", "NotStarted", classId, 0, 0, 0, false, null); }
        catch (Exception exception) when (exception is Win32Exception or IOException)
        { return new("WorkerUnavailable", "NotStarted", classId, 0, 0, 0, false, null); }
        process.StandardInput.Close();
        void Kill()
        {
            try { if (!process.HasExited) process.Kill(true); }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception) { }
        }
        using var cancellation = stopped.Token.Register(Kill);
        async Task Read(Stream stream, bool protocol)
        {
            var buffer = new byte[2048];
            var received = 0;
            using var line = new MemoryStream();
            try
            {
                int count;
                while ((count = await stream.ReadAsync(buffer, stopped.Token)) > 0)
                {
                    received += count;
                    if (received > MaxOutputBytes) { overflow = true; stopped.Cancel(); return; }
                    if (!protocol) continue;
                    for (var index = 0; index < count; index++)
                    {
                        if (buffer[index] == '\n') { parser.Accept(line.ToArray()); line.SetLength(0); }
                        else line.WriteByte(buffer[index]);
                    }
                }
                if (protocol && line.Length != 0) parser.Invalid = true;
            }
            catch (OperationCanceledException) when (stopped.IsCancellationRequested) { }
            catch (IOException) { parser.Invalid = true; }
        }
        var stdout = Read(process.StandardOutput.BaseStream, true);
        var stderr = Read(process.StandardError.BaseStream, false);
        try { await Task.WhenAll(process.WaitForExitAsync(stopped.Token), stdout, stderr); }
        catch (OperationCanceledException) when (stopped.IsCancellationRequested) { }
        finally { Kill(); }
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        if (cancellationToken.IsCancellationRequested) return new("Cancelled", parser.Stage, classId, parser.Frames, parser.SampleRate, parser.Peak, parser.ControllerConnected, null, process.ExitCode);
        if (overflow) return new("OutputLimit", parser.Stage, classId, parser.Frames, parser.SampleRate, parser.Peak, parser.ControllerConnected, null, process.ExitCode);
        if (deadline.IsCancellationRequested) return new("TimedOut", parser.Stage, classId, parser.Frames, parser.SampleRate, parser.Peak, parser.ControllerConnected, null, process.ExitCode);
        var outcome = parser.Result(process.ExitCode);
        if (outcome.Status != "Completed") return outcome;
        try
        {
            var wave = await File.ReadAllBytesAsync(wavePath, cancellationToken);
            if (!WaveMatches(wave, parser.Peak)) return new("InvalidWorkerOutput", parser.Stage, classId, parser.Frames, parser.SampleRate, parser.Peak, parser.ControllerConnected, null, process.ExitCode);
            return outcome with { Wave = wave };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return new("PreviewWriteFailed", parser.Stage, classId, parser.Frames, parser.SampleRate, parser.Peak, parser.ControllerConnected, null, process.ExitCode); }
    }

    public static bool WaveMatches(byte[] wave, int peak)
    {
        var payload = ExpectedFrames * 4;
        if (wave.Length != 44 + payload || peak is < 0 or > 32767) return false;
        if (wave[0] != 'R' || wave[1] != 'I' || wave[2] != 'F' || wave[3] != 'F') return false;
        var heard = 0;
        for (var index = 44; index + 1 < wave.Length; index += 2)
        {
            var sample = wave[index] | (wave[index + 1] << 8);
            if (sample >= 0x8000) sample -= 0x10000;
            var magnitude = Math.Abs(sample);
            if (magnitude > heard) heard = magnitude;
        }
        return heard == peak;
    }

    private sealed class Parser(string classId)
    {
        private static readonly string[] Stages = ["WorkerStarted", "BundleOpened", "ModuleLoaded", "EntryPointsResolved",
            "ModuleEntered", "ComponentCreated", "ComponentInitialized", "ProcessorPrepared", "PreviewRendered",
            "ComponentTerminated", "ModuleExited", "ModuleUnloaded"];
        private static readonly HashSet<string> Failures = ["FactoryUnavailable", "FactoryClassLimit", "FactoryClassReadFailed",
            "ComponentClassUnavailable", "ComponentClassUnsupported", "ComponentCreateFailed", "ComponentInitializeFailed",
            "ProcessorUnavailable", "ProcessingSetupFailed", "ProcessingFailed", "PreviewWriteFailed", "ComponentTerminateFailed", "ExitRejected"];
        private int index = -1;
        private bool preview;
        private bool complete;
        private string? failure;
        public bool Invalid;
        public int Frames, SampleRate, Peak;
        public bool ControllerConnected;
        public string Stage => index >= 0 ? Stages[index] : "NotStarted";

        public void Accept(byte[] bytes)
        {
            if (Invalid) return;
            try
            {
                using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 4 });
                var root = document.RootElement;
                if (root.GetProperty("protocolVersion").GetInt32() != 1) { Invalid = true; return; }
                var status = root.GetProperty("status").GetString();
                var stage = root.GetProperty("stage").GetString();
                var properties = root.EnumerateObject().Count();
                if (status == "Progress" && properties == 3 && index < 7 && stage == Stages[index + 1]) index++;
                else if (status == "Preview" && properties == 8 && index == 7 && !preview && stage == "PreviewRendered" &&
                    root.GetProperty("frames").GetInt32() == ExpectedFrames && root.GetProperty("sampleRate").GetInt32() == ExpectedRate &&
                    root.GetProperty("channels").GetInt32() == 2)
                {
                    Frames = ExpectedFrames;
                    SampleRate = ExpectedRate;
                    Peak = root.GetProperty("peak").GetInt32();
                    ControllerConnected = root.GetProperty("controllerConnected").GetBoolean();
                    if (Peak is < 0 or > 32767) { Invalid = true; return; }
                    preview = true;
                }
                else if (status == "Progress" && properties == 3 && preview && index is >= 7 and <= 9 && stage == Stages[index + 1]) index++;
                else if (status is not null && Failures.Contains(status) && properties == 3 && !complete && stage == Stage) failure = status;
                else if (status == "Completed" && properties == 3 && preview && !complete && index == 10 && stage == "ModuleUnloaded")
                { index++; complete = true; }
                else Invalid = true;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            { Invalid = true; }
        }

        public Vst3PreviewOutcome Result(int exitCode) => Invalid
            ? new("InvalidWorkerOutput", Stage, classId, Frames, SampleRate, Peak, ControllerConnected, null, exitCode)
            : failure is not null && exitCode == 1
                ? new(failure, Stage, classId, Frames, SampleRate, Peak, ControllerConnected, null, exitCode)
                : complete && exitCode == 0
                    ? new("Completed", Stage, classId, Frames, SampleRate, Peak, ControllerConnected, null, exitCode)
                    : new(exitCode != 0 ? "WorkerCrashed" : "InvalidWorkerOutput", Stage, classId, Frames, SampleRate, Peak, ControllerConnected, null, exitCode);
    }
}
