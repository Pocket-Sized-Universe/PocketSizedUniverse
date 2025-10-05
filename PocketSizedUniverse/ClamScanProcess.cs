using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using ECommons.DalamudServices;
using PocketSizedUniverse.Models;

namespace PocketSizedUniverse;

public class ClamScanProcess : Process
{
    private readonly string _exePath =
        Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "clamscan.exe");

    public event EventHandler<FileScannedEventArgs>? FileScanned;

    public readonly System.Timers.Timer ScanTimer;

    private ConcurrentBag<string> _pathsToScan = [];

    // Regex to parse clamscan output
    // Captures: Group 1 = file path, Group 2 = status (virus name or "OK")
    private static readonly Regex ScanOutputRegex = new Regex(
        @"^(.+?):\s+(.+)$",
        RegexOptions.Compiled
    );

    public ClamScanProcess()
    {
        StartInfo = new ProcessStartInfo(_exePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Svc.PluginInterface.AssemblyLocation.DirectoryName
        };
        OutputDataReceived += OnOutput;
        ErrorDataReceived += OnError;
        ScanTimer = new System.Timers.Timer(5000);
        ScanTimer.AutoReset = false;
        ScanTimer.Elapsed += OnTimerElapsed;
    }

    public void EnqueuePath(string path)
    {
        _pathsToScan.Add(path);
        ScanTimer.Stop();
        ScanTimer.Start();
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        List<string> pathsToProcess = _pathsToScan.ToList();
        _pathsToScan.Clear();
        StartScan(pathsToProcess);
    }

    private void StartScan(List<string> paths)
    {
        string fileList = string.Join(Environment.NewLine, paths);
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, fileList);
        string args = $"--no-summary --database=\"{PsuPlugin.ClamDbPath}\" --file-list=\"{tempFile}\"";
        StartInfo.Arguments = args;
        Start();
        BeginOutputReadLine();
        BeginErrorReadLine();
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