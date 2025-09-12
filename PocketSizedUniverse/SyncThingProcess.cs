using System.Diagnostics;
using ECommons.DalamudServices;
using PocketSizedUniverse.Services;

namespace PocketSizedUniverse;

public class SyncThingProcess : Process
{
    public SyncThingProcess(string args)
    {
        StartInfo = new ProcessStartInfo(Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName,
            "syncthing.exe"))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Arguments = args,
        };
        OutputDataReceived += OnOutput;
        ErrorDataReceived += OnError;
    }

    private void OnError(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;
        Svc.Log.Error($"ST: {e.Data}");
    }

    private void OnOutput(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;
        Svc.Log.Debug($"ST: {e.Data}");
    }
}