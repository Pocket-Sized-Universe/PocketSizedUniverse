using System.Diagnostics;
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
    public static CustomizeService CustomizeService;
    public static HonorificService HonorificService;
    public static SimpleHeelsService SimpleHeelsService;
    public static WindowSystem WindowSystem;
    public static MainWindow MainWindow;
    public static SetupWindow SetupWindow;
    public static ProgressWindow ProgressWindow;
    public static SyncThingService SyncThingService;
    public static MoodlesService MoodlesService;
    public static PlayerDataService PlayerDataService;
    public static ContextMenuService ContextMenuService;
    public static SyncThingProcess? ServerProcess;
    public static FreshclamProcess FreshclamProcess;
    public static ClamScanProcess? ClamScanProcess;
    public static readonly string ClamDbPath = Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "clam_db");

    public PsuPlugin(IDalamudPluginInterface dalamudPluginInterface)
    {
        ECommonsMain.Init(dalamudPluginInterface, this, Module.All);
        Configuration = EzConfig.Init<Configuration>();

        if (Configuration.UseBuiltInSyncThing && Configuration.SetupComplete)
        {
            StartServer();
        }

        FreshclamProcess = new FreshclamProcess();
        FreshclamProcess.Start();
        FreshclamProcess.BeginOutputReadLine();
        FreshclamProcess.BeginErrorReadLine();
        Task.Run(() =>
        {
            FreshclamProcess.WaitForExit();
            if (FreshclamProcess.ExitCode != 0)
            {
                Svc.Log.Error("Failed to update ClamAV database");
                return;
            }
            Svc.Log.Information("ClamAV database updated successfully");
            ClamScanProcess = new ClamScanProcess();
            ClamScanProcess.FileScanned += OnFileScanned;
        });

        PenumbraService = new PenumbraService();
        GlamourerService = new GlamourerService();
        CustomizeService = new CustomizeService();
        HonorificService = new HonorificService();
        MoodlesService = new MoodlesService();
        SimpleHeelsService = new SimpleHeelsService();

        SyncThingService = new SyncThingService();

        PlayerDataService = new PlayerDataService();

        ContextMenuService = new ContextMenuService();

        WindowSystem = new WindowSystem();
        MainWindow = new MainWindow();
        SetupWindow = new SetupWindow();
        ProgressWindow = new ProgressWindow();
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(SetupWindow);
        WindowSystem.AddWindow(ProgressWindow);

        if (!Configuration.SetupComplete)
        {
            SetupWindow.IsOpen = true;
        }

        Svc.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi += MainWindow.Toggle;

        Svc.Log.Information(
            $"Pocket Sized Universe plugin loaded | WINE: {IsRunningUnderWine()} | Easy Mode: {Configuration.UseBuiltInSyncThing} | Server Process Exited: {ServerProcess?.HasExited ?? true}");
    }

    private void OnFileScanned(object? sender, ClamScanProcess.FileScannedEventArgs e)
    {
        Configuration.ScanResults[e.FilePath] = e.Result;
        EzConfig.Save();
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

        var args =
            $"--home=\"{homePath}\" --gui-address={Configuration.ApiUri?.ToString()} --gui-apikey={Configuration.ApiKey} --no-browser";
        ServerProcess = new SyncThingProcess(args);
        ServerProcess.Start();
        ServerProcess.BeginOutputReadLine();
        ServerProcess.BeginErrorReadLine();
    }

    public static void StopServer()
    {
        if (Configuration.UseBuiltInSyncThing && ServerProcess != null)
        {
            SyncThingService.Shutdown();
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
        StopServer();
        PlayerDataService.Dispose();
        SyncThingService.Dispose();
        ECommonsMain.Dispose();
    }

    public static bool IsRunningUnderWine()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINE_PREFIX")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINEPREFIX")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINELOADER")))
        {
            return true;
        }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Wine");
            return key != null;
        }
        catch
        {
            return false;
        }
    }
}