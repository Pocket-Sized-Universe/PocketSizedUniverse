using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using nClam;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace PocketSizedUniverse.Services;

public class AntiVirusService : IDisposable
{
    private readonly string _databasePath = Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "clam_db");
    private readonly string _freshclamConfig = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "freshclam.conf");
    private readonly string _clamdConfig = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "clamd.conf");
    
    private FreshclamProcess _freshclamProcess;
    
    private ClamDProcess _engine;
    
    private ClamClient? _client;

    private Task _initTask;
    
    private ConcurrentDictionary<string, (DateTime LastWriteTime, DateTime ScanTime, ClamScanResult Result)> ScanResults { get; } = new();
    private List<string> _scansInFlight = new();
    public AntiVirusService()
    {
        string args = $"--config-file=\"{_freshclamConfig}\" --datadir=\"{_databasePath}\"";
        _freshclamProcess = new FreshclamProcess(args);
        _freshclamProcess.Start();
        _freshclamProcess.BeginOutputReadLine();
        _freshclamProcess.BeginErrorReadLine();
        string daemonArgs = $"--config-file=\"{_clamdConfig}\" --datadir=\"{_databasePath}\" --foreground";
        _engine = new ClamDProcess(daemonArgs);
        _initTask = Task.Run(() =>
        {
            _freshclamProcess.WaitForExit();
            if (_freshclamProcess.ExitCode != 0)
            {
                Svc.Log.Error("Failed to initialize ClamAV database. Exit code: " + _freshclamProcess.ExitCode);
                return;
            }
            _engine.Start();
            _engine.BeginOutputReadLine();
            _engine.BeginErrorReadLine();
            _client = new ClamClient("127.0.0.1");
            Svc.Log.Information("ClamAV engine initialized.");
        });
    }

    public Result IsFileSafe(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists)
            return Result.Clean;

        if (ScanResults.TryGetValue(fullPath, out var result))
        {
            if (result.LastWriteTime != fileInfo.LastWriteTimeUtc)
            {
                Task.Run(() => ScanFile(fullPath));
                return Result.WaitForScan;
            }
            
            if (result.ScanTime.AddMinutes(10) < DateTime.UtcNow)
            {
                Task.Run(() => ScanFile(fullPath));
                return Result.WaitForScan;
            }
            
            return result.Result.Result == ClamScanResults.Clean ? Result.Clean : Result.Infected;
        }
        
        Task.Run(() => ScanFile(fullPath));
        return Result.WaitForScan;
    }

    private void ScanFile(string path)
    {
        if (!_initTask.IsCompleted || _client == null)
        {
            Svc.Log.Warning("ClamAV engine not initialized yet. Cannot scan file.");
            return;
        }

        if (_scansInFlight.Contains(path))
            return;

        Task.Run( async () =>
        {
            _scansInFlight.Add(path);
            var fileInfo = new FileInfo(path);
            var result = await _client.ScanFileOnServerAsync(path);
            Svc.Log.Debug($"Scanned file {path}. Result: {result}");
            ScanResults[path] = (fileInfo.LastWriteTimeUtc, DateTime.UtcNow, result);
            _scansInFlight.Remove(path);
        });
    }

    public enum Result
    {
        Clean,
        Infected,
        WaitForScan,
    }

    public void Dispose()
    {
        _freshclamProcess.Kill();
        _freshclamProcess.Dispose();
        _engine.Kill();
        _engine.Dispose();
        _initTask.Dispose();
    }
}