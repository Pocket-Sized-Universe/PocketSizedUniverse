using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Commands;
using ECommons.Configuration;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PocketSizedUniverse.Services;
using PocketSizedUniverse.Windows;
using Syncthing;
using Syncthing.Http;

namespace PocketSizedUniverse;

public class PsuPlugin : IDalamudPlugin
{
    public static Configuration Configuration;
    public static PenumbraService PenumbraService;
    public static GlamourerService GlamourerService;
    public static WindowSystem WindowSystem;
    public static MainWindow MainWindow;
    public static SetupWindow SetupWindow;
    public static SyncThingService SyncThingService;
    public static PlayerDataService PlayerDataService;
    public static SyncThingProcess? ServerProcess;
    private Task _serverRunTask;
    public PsuPlugin(IDalamudPluginInterface dalamudPluginInterface)
    {
        ECommonsMain.Init(dalamudPluginInterface, this, Module.All);
        Configuration = EzConfig.Init<Configuration>();

        if (Configuration.UseBuiltInSyncThing && Configuration.SetupComplete)
        {
            StartServer();
        }

        PenumbraService = new PenumbraService();
        GlamourerService = new GlamourerService();

        SyncThingService = new SyncThingService();

        PlayerDataService = new PlayerDataService();

        WindowSystem = new WindowSystem();
        MainWindow = new MainWindow();
        SetupWindow = new SetupWindow();
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(SetupWindow);
        
        if (!Configuration.SetupComplete)
        {
            SetupWindow.IsOpen = true;
        }
        Svc.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi += MainWindow.Toggle;
    }

    public static void StartServer()
    {
        var homePath = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "engine-home");
        Directory.CreateDirectory(homePath);

        if (ServerProcess is { HasExited: false })
        {
            Svc.Log.Debug("Server process already running, nothing to start");
            return;
        }

        var args = $"--home=\"{homePath}\" --gui-address={Configuration.ApiUri?.ToString()} --gui-apikey={Configuration.ApiKey} --no-browser";
        ServerProcess = new SyncThingProcess(args);
        ServerProcess.Start();
        ServerProcess.BeginOutputReadLine();
        ServerProcess.BeginErrorReadLine();
    }

    public static void StopServer()
    {
        if (ServerProcess == null || ServerProcess.HasExited)
        {
            Svc.Log.Debug("Server process not running, nothing to stop");
            return;
        }
        
        try
        {
            var homePath = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "engine-home");
            var stopArgs = $"{SyncThingProcessStartType.Stop.ToArgument()}--home=\"{homePath}\" --gui-address={Configuration.ApiUri?.Host}:{Configuration.ApiUri?.Port} --gui-apikey={Configuration.ApiKey}";
            var stopProcess = new SyncThingProcess(stopArgs);
            stopProcess.Start();
            stopProcess.BeginOutputReadLine();
            stopProcess.BeginErrorReadLine();
            stopProcess.WaitForExit();
            ServerProcess.WaitForExit();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to stop syncthing process: {ex}");
        }
    }

    [Cmd("/psu", "/psu help for more info")]
    public void OnCommand(string command, string args)
    {
        if (string.Equals(args, "help", StringComparison.OrdinalIgnoreCase))
        {
            Svc.Chat.Print("Here be dragons (TODO)");
            return;
        }

        if (Configuration.SetupComplete)
            MainWindow.Toggle();
    }

    public void Dispose()
    {
        if (!ServerProcess?.HasExited ?? true)
            StopServer();
        ECommonsMain.Dispose();
    }
}