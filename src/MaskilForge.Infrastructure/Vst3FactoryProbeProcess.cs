using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace MaskilForge.Infrastructure;

public sealed record Vst3FactoryClass(string Id, string Name, string Category);
public sealed record Vst3FactoryProbeOutcome(string Status, string LastCompletedStage, IReadOnlyList<Vst3FactoryClass> Classes, int? ExitCode = null);

/// <summary>Supervises read-only VST3 factory class enumeration. It never creates a component instance.</summary>
public sealed class Vst3FactoryProbeProcess(TimeSpan? timeout = null)
{
    private const int MaxOutputBytes = 64 * 1024;
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(10);

    public async Task<Vst3FactoryProbeOutcome> RunAsync(string worker, string bundle, string binary, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(_timeout);
        using var stopped = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        using var process = new Process { StartInfo = new(worker) {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = true, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(worker)!
        } };
        process.StartInfo.ArgumentList.Add(bundle); process.StartInfo.ArgumentList.Add(binary); process.StartInfo.ArgumentList.Add("--enumerate-factory");
        process.StartInfo.Environment.Clear();
        foreach (var name in new[] { "HOME", "TMPDIR", "LANG" })
            if (Environment.GetEnvironmentVariable(name) is { } value) process.StartInfo.Environment[name] = value;
        process.StartInfo.Environment["PATH"] = "/usr/bin:/bin";
        var parser = new Parser(); var overflow = false;
        try { if (!process.Start()) return new("WorkerUnavailable", "NotStarted", []); }
        catch (Exception ex) when (ex is Win32Exception or IOException) { return new("WorkerUnavailable", "NotStarted", []); }
        process.StandardInput.Close();
        void Kill() { try { if (!process.HasExited) process.Kill(true); } catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { } }
        using var cancellation = stopped.Token.Register(Kill);
        async Task Read(Stream stream, bool protocol)
        {
            var buffer = new byte[2048]; var received = 0; using var line = new MemoryStream();
            try {
                int count;
                while ((count = await stream.ReadAsync(buffer, stopped.Token)) > 0) {
                    received += count; if (received > MaxOutputBytes) { overflow = true; stopped.Cancel(); return; }
                    if (!protocol) continue;
                    for (var i = 0; i < count; i++) { if (buffer[i] == '\n') { parser.Accept(line.ToArray()); line.SetLength(0); } else line.WriteByte(buffer[i]); }
                }
                if (protocol && line.Length != 0) parser.Invalid = true;
            } catch (OperationCanceledException) when (stopped.IsCancellationRequested) { } catch (IOException) { parser.Invalid = true; }
        }
        var stdout = Read(process.StandardOutput.BaseStream, true); var stderr = Read(process.StandardError.BaseStream, false);
        try { await Task.WhenAll(process.WaitForExitAsync(stopped.Token), stdout, stderr); } catch (OperationCanceledException) when (stopped.IsCancellationRequested) { }
        finally { Kill(); }
        await process.WaitForExitAsync(); await Task.WhenAll(stdout, stderr);
        if (cancellationToken.IsCancellationRequested) return new("Cancelled", parser.Stage, parser.Classes, process.ExitCode);
        if (overflow) return new("OutputLimit", parser.Stage, parser.Classes, process.ExitCode);
        if (deadline.IsCancellationRequested) return new("TimedOut", parser.Stage, parser.Classes, process.ExitCode);
        return parser.Result(process.ExitCode);
    }

    private sealed class Parser
    {
        private static readonly string[] Stages = ["WorkerStarted", "BundleOpened", "ModuleLoaded", "EntryPointsResolved", "ModuleEntered", "ModuleExited", "ModuleUnloaded"];
        private int index = -1; private bool factoryComplete; private bool complete; private string? failure; public bool Invalid; public readonly List<Vst3FactoryClass> Classes = [];
        public string Stage => index >= 0 ? Stages[index] : "NotStarted";
        public void Accept(byte[] bytes)
        {
            if (Invalid) return;
            try {
                using var doc = JsonDocument.Parse(bytes, new() { MaxDepth = 4 }); var root = doc.RootElement;
                if (root.GetProperty("protocolVersion").GetInt32() != 1) { Invalid = true; return; }
                var status = root.GetProperty("status").GetString(); var stage = root.GetProperty("stage").GetString(); var properties = root.EnumerateObject().Count();
                if (status == "Progress" && properties == 3 && index < 4 && stage == Stages[index + 1]) index++;
                else if (status == "Class" && properties == 6 && index == 4 && !factoryComplete && Classes.Count < 256 && stage == "FactoryClass")
                {
                    var id = root.GetProperty("id").GetString(); var name = root.GetProperty("name").GetString(); var category = root.GetProperty("category").GetString();
                    if (id?.Length != 32 || !id.All(Uri.IsHexDigit) || name is null || name.Length > 64 || category is null || category.Length > 32) { Invalid = true; return; }
                    Classes.Add(new(id, name, category));
                }
                else if (status == "FactoryCompleted" && properties == 4 && index == 4 && !factoryComplete && stage == "FactoryClasses" && root.GetProperty("classCount").GetInt32() == Classes.Count) factoryComplete = true;
                else if (status == "Progress" && properties == 3 && factoryComplete && index == 4 && stage == "ModuleExited") index++;
                else if (status is "FactoryUnavailable" or "FactoryClassLimit" or "FactoryClassReadFailed" && properties == 3 && index == 4 && stage == Stage) failure = status;
                else if (status == "Completed" && properties == 3 && factoryComplete && !complete && stage == "ModuleUnloaded" && index == 5) { index++; complete = true; }
                else Invalid = true;
            } catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { Invalid = true; }
        }
        public Vst3FactoryProbeOutcome Result(int exitCode) => Invalid ? new("InvalidWorkerOutput", Stage, Classes, exitCode) : failure is not null && exitCode == 1 ? new(failure, Stage, Classes, exitCode) : complete && exitCode == 0 ? new("Completed", Stage, Classes, exitCode) : new(exitCode != 0 ? "WorkerCrashed" : "InvalidWorkerOutput", Stage, Classes, exitCode);
    }
}
