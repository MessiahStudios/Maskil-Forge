using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace MaskilForge.Infrastructure;

public sealed record Vst3AudioBus(string Direction, int Index, string Name, int ChannelCount, string BusType,
    bool DefaultActive, bool ControlVoltage);
public sealed record Vst3ComponentProbeOutcome(string Status, string LastCompletedStage, string ClassId,
    IReadOnlyList<Vst3AudioBus> Buses, int? ExitCode = null);

/// <summary>Supervises creation, initialization, audio-bus inspection, and termination of one VST3 component.</summary>
public sealed class Vst3ComponentProbeProcess(TimeSpan? timeout = null)
{
    private const int MaxOutputBytes = 128 * 1024;
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(10);

    public async Task<Vst3ComponentProbeOutcome> RunAsync(string worker, string bundle, string binary, string classId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(classId) || classId.Length != 32 || !classId.All(Uri.IsHexDigit))
            return new("InvalidClassId", "NotStarted", classId, []);
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
        process.StartInfo.ArgumentList.Add("--inspect-component");
        process.StartInfo.ArgumentList.Add(classId);
        process.StartInfo.Environment.Clear();
        foreach (var name in new[] { "HOME", "TMPDIR", "LANG" })
            if (Environment.GetEnvironmentVariable(name) is { } value) process.StartInfo.Environment[name] = value;
        process.StartInfo.Environment["PATH"] = "/usr/bin:/bin";
        var parser = new Parser(classId);
        var overflow = false;
        try { if (!process.Start()) return new("WorkerUnavailable", "NotStarted", classId, []); }
        catch (Exception exception) when (exception is Win32Exception or IOException)
        { return new("WorkerUnavailable", "NotStarted", classId, []); }
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
        if (cancellationToken.IsCancellationRequested) return new("Cancelled", parser.Stage, classId, parser.Buses, process.ExitCode);
        if (overflow) return new("OutputLimit", parser.Stage, classId, parser.Buses, process.ExitCode);
        if (deadline.IsCancellationRequested) return new("TimedOut", parser.Stage, classId, parser.Buses, process.ExitCode);
        return parser.Result(process.ExitCode);
    }

    private sealed class Parser(string classId)
    {
        private static readonly string[] Stages = ["WorkerStarted", "BundleOpened", "ModuleLoaded", "EntryPointsResolved",
            "ModuleEntered", "ComponentCreated", "ComponentInitialized", "BusesInspected", "ComponentTerminated", "ModuleExited", "ModuleUnloaded"];
        private static readonly HashSet<string> Failures = ["FactoryUnavailable", "FactoryClassLimit", "FactoryClassReadFailed",
            "ComponentClassUnavailable", "ComponentClassUnsupported", "ComponentCreateFailed", "ComponentInitializeFailed",
            "AudioBusLimit", "AudioBusReadFailed", "ComponentTerminateFailed", "ExitRejected"];
        private int index = -1;
        private bool componentComplete;
        private bool complete;
        private string? failure;
        public bool Invalid;
        public readonly List<Vst3AudioBus> Buses = [];
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
                if (status == "Progress" && properties == 3 && index < 6 && stage == Stages[index + 1]) index++;
                else if (status == "Bus" && properties == 10 && index == 6 && !componentComplete && stage == "AudioBus")
                {
                    var direction = root.GetProperty("direction").GetString();
                    var busIndex = root.GetProperty("index").GetInt32();
                    var name = root.GetProperty("name").GetString();
                    var channels = root.GetProperty("channelCount").GetInt32();
                    var type = root.GetProperty("busType").GetString();
                    var expectedIndex = Buses.Count(bus => bus.Direction == direction);
                    var outputAlreadyStarted = Buses.Any(bus => bus.Direction == "Output");
                    if (direction is not ("Input" or "Output") || busIndex != expectedIndex ||
                        direction == "Input" && outputAlreadyStarted || name is null || name.Length > 256 ||
                        channels is < 0 or > 1024 || type is not ("Main" or "Aux") || Buses.Count >= 128)
                    { Invalid = true; return; }
                    Buses.Add(new(direction, busIndex, name, channels, type,
                        root.GetProperty("defaultActive").GetBoolean(), root.GetProperty("controlVoltage").GetBoolean()));
                }
                else if (status == "ComponentCompleted" && properties == 6 && index == 6 && !componentComplete &&
                    stage == "AudioBuses" && root.GetProperty("classId").GetString() == classId &&
                    root.GetProperty("inputBusCount").GetInt32() == Buses.Count(bus => bus.Direction == "Input") &&
                    root.GetProperty("outputBusCount").GetInt32() == Buses.Count(bus => bus.Direction == "Output")) componentComplete = true;
                else if (status == "Progress" && properties == 3 && componentComplete && index == 6 && stage == "BusesInspected") index++;
                else if (status == "Progress" && properties == 3 && componentComplete && index is 7 or 8 && stage == Stages[index + 1]) index++;
                else if (status is not null && Failures.Contains(status) && properties == 3 && !complete && stage == Stage) failure = status;
                else if (status == "Completed" && properties == 3 && componentComplete && !complete && index == 9 && stage == "ModuleUnloaded")
                { index++; complete = true; }
                else Invalid = true;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            { Invalid = true; }
        }

        public Vst3ComponentProbeOutcome Result(int exitCode) => Invalid
            ? new("InvalidWorkerOutput", Stage, classId, Buses, exitCode)
            : failure is not null && exitCode == 1
                ? new(failure, Stage, classId, Buses, exitCode)
                : complete && exitCode == 0
                    ? new("Completed", Stage, classId, Buses, exitCode)
                    : new(exitCode != 0 ? "WorkerCrashed" : "InvalidWorkerOutput", Stage, classId, Buses, exitCode);
    }
}
