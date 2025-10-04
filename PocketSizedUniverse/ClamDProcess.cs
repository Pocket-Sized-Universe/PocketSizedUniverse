using System.Diagnostics;
using ECommons.DalamudServices;

namespace PocketSizedUniverse;

public class ClamDProcess : Process
{
    private readonly string _exePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "clamd.exe");
    
    public ClamDProcess(string args)
    {
        StartInfo = new ProcessStartInfo(_exePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Arguments = args,
            WorkingDirectory = Svc.PluginInterface.AssemblyLocation.DirectoryName
        };
        OutputDataReceived += OnOutput;
        ErrorDataReceived += OnError;
    }
    
    private void OnError(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;
        Svc.Log.Error($"ClamD: {e.Data}");
    }

    private void OnOutput(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;
        Svc.Log.Debug($"ClamD: {e.Data}");
    }
}