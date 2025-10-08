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

    public readonly System.Timers.Timer ScanTimer;

    public readonly ConcurrentBag<string> PathsToScan = [];

    public Process? ScannerProcess;

    // Regex to parse clamscan output
    // Captures: Group 1 = file path, Group 2 = status (virus name or "OK")
    private static readonly Regex ScanOutputRegex = new Regex(
        @"^(.+?):\s+(.+)$",
        RegexOptions.Compiled
    );

    public AntiVirusScanner()
    {
        ScanTimer = new System.Timers.Timer(5000);
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
        PsuPlugin.FreshclamProcess.WaitForExit();
        string? tempList = null;
        if (ScannerProcess != null)
        {
            Svc.Log.Warning("ClamScan process already running, skipping scan");
            return;
        }
        try
        {
            string fileList = string.Join(Environment.NewLine, paths);
            tempList = Path.GetTempFileName();
            File.WriteAllText(tempList, fileList);
            string args = $"--no-summary --database=\"{PsuPlugin.ClamDbPath}\" --file-list=\"{tempList}\"";
            ScannerProcess = new Process()
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
            ScannerProcess.OutputDataReceived += OnOutput;
            ScannerProcess.ErrorDataReceived += OnError;
            ScannerProcess.Start();
            ScannerProcess.BeginOutputReadLine();
            ScannerProcess.BeginErrorReadLine();
            ScannerProcess.WaitForExit();
            Svc.Log.Information("ClamScan scan completed");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to start ClamScan: {ex}");
        }
        finally
        {
            if (tempList != null && File.Exists(tempList))
                File.Delete(tempList);
            if (ScannerProcess != null)
            {
                ScannerProcess.OutputDataReceived -= OnOutput;
                ScannerProcess.ErrorDataReceived -= OnError;
                ScannerProcess.Dispose();
                ScannerProcess = null;
            }
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