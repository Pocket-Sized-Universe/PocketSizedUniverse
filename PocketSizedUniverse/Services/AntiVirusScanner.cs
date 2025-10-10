using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using ECommons.DalamudServices;
using PocketSizedUniverse.Models;

namespace PocketSizedUniverse.Services;

public class AntiVirusScanner
{
    private readonly string _exePath =
        Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "clamscan.exe");

    public event EventHandler<FileScannedEventArgs>? FileScanned;

    public event EventHandler ScanCompleted;

    public readonly System.Timers.Timer ScanTimer;

    public readonly ConcurrentBag<string> PathsToScan = [];

    // Regex to parse clamscan output
    // Captures: Group 1 = file path, Group 2 = status (virus name or "OK")
    private static readonly Regex ScanOutputRegex = new Regex(
        @"^(.+?):\s+(.+)$",
        RegexOptions.Compiled
    );

    public AntiVirusScanner()
    {
        ScanTimer = new System.Timers.Timer(15000);
        ScanTimer.AutoReset = false;
        ScanTimer.Elapsed += OnTimerElapsed;
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        List<string> pathsToProcess = PathsToScan.ToList();
        PathsToScan.Clear();
        Task.Run(() => StartScan(pathsToProcess));
    }

    public void EnqueuePath(string path)
    {
        PathsToScan.Add(path);
        ScanTimer.Stop();
        ScanTimer.Start();
    }

    private void StartScan(List<string> paths)
    {
        if (!PsuPlugin.FreshclamProcess.HasExited)
        {
            Svc.Log.Warning("ClamAV database update still running, deferring scan");
            foreach (var path in paths)
                PathsToScan.Add(path);
            ScanTimer.Start();
            return;
        }

        string? tempList = null;
        try
        {
            string fileList = string.Join(Environment.NewLine, paths);
            tempList = Path.GetTempFileName();
            File.WriteAllText(tempList, fileList);
            string args = $"--no-summary --database=\"{PsuPlugin.ClamDbPath}\" --file-list=\"{tempList}\"";

            var tempListPath = tempList; // Capture for closure
            var process = new Process
            {
                StartInfo = new ProcessStartInfo(_exePath)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Arguments = args,
                    WorkingDirectory = Svc.PluginInterface.AssemblyLocation.DirectoryName
                }
            };

            process.OutputDataReceived += OnOutput;
            process.ErrorDataReceived += OnError;
            process.Exited += (sender, eventArgs) =>
            {
                Svc.Log.Information("ClamScan scan completed");
                if (File.Exists(tempListPath))
                    File.Delete(tempListPath);

                process.OutputDataReceived -= OnOutput;
                process.ErrorDataReceived -= OnError;
                process.Dispose();
                ScanCompleted?.Invoke(this, EventArgs.Empty);
            };

            process.EnableRaisingEvents = true;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to start ClamScan: {ex}");

            // Cleanup on error
            if (tempList != null && File.Exists(tempList))
                File.Delete(tempList);
        }
    }

    private void OnOutput(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;
        Svc.Log.Verbose($"ClamScan: {e.Data}");

        var parsed = ParseScanOutput(e.Data);
        if (parsed == null) return;

        FileScanned?.Invoke(this, new FileScannedEventArgs(parsed.Value.path, parsed.Value.result));
    }

    private void OnError(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;
        Svc.Log.Error($"ClamScan: {e.Data}");
    }

    private static (string path, ScanResult result)? ParseScanOutput(string output)
    {
        var match = ScanOutputRegex.Match(output);
        if (!match.Success)
            return null;

        var filePath = match.Groups[1].Value.Trim();
        var statusText = match.Groups[2].Value.Trim();

        ScanResult result;

        if (string.Equals(statusText, "OK", StringComparison.OrdinalIgnoreCase))
        {
            result = new ScanResult()
            {
                Result = ScanResult.ResultType.Clean,
                MalwareIdentifier = null
            };
        }
        else if (statusText.EndsWith("FOUND", StringComparison.OrdinalIgnoreCase))
        {
            var malwareId = statusText.Substring(0, statusText.Length - "FOUND".Length).Trim();

            result = new ScanResult()
            {
                Result = ScanResult.ResultType.Infected,
                MalwareIdentifier = malwareId
            };
        }
        else
            return null;

        return (filePath, result);
    }

    public class FileScannedEventArgs(string filePath, ScanResult result) : EventArgs
    {
        public string FilePath { get; set; } = filePath;
        public ScanResult Result { get; set; } = result;
    }
}