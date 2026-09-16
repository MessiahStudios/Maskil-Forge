using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace MaskilForge.Infrastructure;

public sealed record Vst3ProbeOutcome(string Status, string LastCompletedStage, int? ExitCode = null);

/// <summary>Crash containment, not a security sandbox. Third-party code runs with the user's privileges.</summary>
public sealed class Vst3ProbeProcess(TimeSpan? timeout = null)
{
    public const int MaxOutputBytes = 16 * 1024;
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(10);
    private static readonly string[] Stages = ["WorkerStarted", "BundleOpened", "ModuleLoaded", "EntryPointsResolved", "ModuleEntered", "ModuleExited", "ModuleUnloaded"];
    private static readonly Dictionary<string, string> Failures = new()
    {
        ["BundleOpenFailed"] = "WorkerStarted", ["ExecutableChanged"] = "WorkerStarted", ["LoadFailed"] = "BundleOpened",
        ["MissingEntryPoints"] = "ModuleLoaded", ["EntryRejected"] = "EntryPointsResolved", ["ExitRejected"] = "ModuleEntered"
    };

    public async Task<Vst3ProbeOutcome> RunAsync(string worker, string bundle, string binary, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(_timeout);
        using var stopped = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        using var process = new Process { StartInfo = new(worker) {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(worker)!
        } };
        process.StartInfo.ArgumentList.Add(bundle);
        process.StartInfo.ArgumentList.Add(binary);
        // Do not propagate service secrets or injected loader settings into third-party code.
        process.StartInfo.Environment.Clear();
        foreach (var name in new[] { "HOME", "TMPDIR", "LANG" })
            if (Environment.GetEnvironmentVariable(name) is { } value) process.StartInfo.Environment[name] = value;
        process.StartInfo.Environment["PATH"] = "/usr/bin:/bin";
        var outputLimit = 0;
        var protocol = new ProbeProtocol();
        try
        {
            if (!process.Start()) return new("WorkerUnavailable", "NotStarted");
        }
        catch (Exception exception) when (exception is Win32Exception or IOException) { return new("WorkerUnavailable", "NotStarted"); }
        process.StandardInput.Close();
        void Kill()
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception) { }
        }
        using var cancellation = stopped.Token.Register(Kill);
        async Task Read(Stream stream, bool isProtocol)
        {
            var bytes = new byte[1024];
            var received = 0;
            using var contents = new MemoryStream();
            try
            {
                int count;
                while ((count = await stream.ReadAsync(bytes, stopped.Token)) > 0)
                {
                    received += count;
                    if (received > MaxOutputBytes) { Interlocked.Exchange(ref outputLimit, 1); stopped.Cancel(); return; }
                    if (!isProtocol) continue; // Drain but never expose third-party logs to browsers or activity logs.
                    for (var index = 0; index < count; index++)
                    {
                        if (bytes[index] == '\n') { protocol.Accept(contents.ToArray()); contents.SetLength(0); }
                        else contents.WriteByte(bytes[index]);
                    }
                }
                if (isProtocol && contents.Length != 0) protocol.Invalidate();
            }
            catch (OperationCanceledException) when (stopped.IsCancellationRequested) { }
            catch (IOException) { if (isProtocol) protocol.Invalidate(); }
        }
        var stdout = Read(process.StandardOutput.BaseStream, true);
        var stderr = Read(process.StandardError.BaseStream, false);
        try { await Task.WhenAll(process.WaitForExitAsync(stopped.Token), stdout, stderr); }
        catch (OperationCanceledException) when (stopped.IsCancellationRequested) { }
        finally { Kill(); }
        // Reap the worker; drain operations also observe the cancellation token, including inherited pipes.
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        if (cancellationToken.IsCancellationRequested) return new("Cancelled", protocol.Stage, process.ExitCode);
        if (outputLimit != 0) return new("OutputLimit", protocol.Stage, process.ExitCode);
        if (deadline.IsCancellationRequested) return new("TimedOut", protocol.Stage, process.ExitCode);
        return protocol.Result(process.ExitCode);
    }

    private sealed class ProbeProtocol
    {
        private int _index = -1;
        private bool _invalid;
        private string? _final;
        public string Stage => _index >= 0 ? Stages[_index] : "NotStarted";
        public void Invalidate() => _invalid = true;
        public void Accept(byte[] bytes)
        {
            if (_invalid || _final is not null) { _invalid = true; return; }
            try
            {
                using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 4 });
                var root = document.RootElement;
                if (root.EnumerateObject().Count() != 3 || root.GetProperty("protocolVersion").GetInt32() != 1) { Invalidate(); return; }
                var status = root.GetProperty("status").GetString();
                var stage = root.GetProperty("stage").GetString();
                if (status == "Progress" && _index < Stages.Length - 2 && stage == Stages[_index + 1]) _index++;
                else if (status == "Completed" && _index == Stages.Length - 2 && stage == Stages[^1]) { _index++; _final = status; }
                else if (status is not null && Failures.TryGetValue(status, out var expected) && stage == Stage && stage == expected) _final = status;
                else Invalidate();
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { Invalidate(); }
        }
        public Vst3ProbeOutcome Result(int exitCode)
        {
            if (_invalid) return new("InvalidWorkerOutput", Stage, exitCode);
            if (_final == "Completed" && exitCode == 0) return new("Completed", Stage, exitCode);
            if (_final is not null && _final != "Completed" && exitCode == 1) return new(_final, Stage, exitCode);
            return new(exitCode != 0 ? "WorkerCrashed" : "InvalidWorkerOutput", Stage, exitCode);
        }
    }
}
