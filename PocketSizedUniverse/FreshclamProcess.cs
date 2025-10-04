using System.Diagnostics;
using ECommons.DalamudServices;
using PocketSizedUniverse.Services;

namespace PocketSizedUniverse;

public class FreshclamProcess : Process
{
    private readonly string _exePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "freshclam.exe");
    public FreshclamProcess(string args)
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
        Svc.Log.Error($"Freshclam: {e.Data}");
    }

    private void OnOutput(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;
        Svc.Log.Debug($"Freshclam: {e.Data}");
    }
}