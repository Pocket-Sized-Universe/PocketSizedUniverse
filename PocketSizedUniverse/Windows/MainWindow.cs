using System.Diagnostics;
using System.Reflection;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using OtterGui;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow : Window
{
    public MainWindow() : base("Pocket Sized Universe " + Assembly.GetExecutingAssembly().GetName().Version!.ToString(3))
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 500),
        };
        TitleBarButtons.Add(new TitleBarButton()
        {
            Icon = FontAwesomeIcon.At,
            Click = (click) =>
            {
                try
                {
                    // Use ProcessStartInfo with the URL directly
                    var psi = new ProcessStartInfo
                    {
                        FileName = "https://discord.gg/2tdUMMMuB5",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    // Log the exception or show an error message
                    Svc.Log.Error($"Failed to open Discord link: {ex}");
                }
            },
            ShowTooltip = () =>
            {
                ImGui.BeginTooltip();
                ImGui.Text("Join the Pocket Sized Universe Discord for support and updates!");
                ImGui.EndTooltip();
            }
        });

    }
    
    public override void Draw()
    {
        DrawTopStatus();
        ImGui.Separator();
        DrawTabs();
    }

    public override bool DrawConditions() => PsuPlugin.Configuration.SetupComplete;

    public override void OnClose()
    {
        PsuPlugin.SyncThingService.LockRefresh = false;
        _dataPackEditorChanged = false;
        _starEditorChanged = false;
        base.OnClose();
    }
}
