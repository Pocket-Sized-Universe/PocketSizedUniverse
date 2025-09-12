using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.Hooks;
using ECommons.ImGuiMethods;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Models.Data;
using PocketSizedUniverse.Services;
using PocketSizedUniverse.Windows.Elements;
using PocketSizedUniverse.Windows.ViewModels;
using Syncthing.Models.Response;

namespace PocketSizedUniverse.Windows;

public class MainWindow : Window
{
    public MainWindow() : base("Pocket Sized Universe " + Assembly.GetExecutingAssembly().GetName().Version)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }
    
    public override void Draw()
    {
        DrawStatusSection();
        ImGui.Separator();
    }

    public override bool DrawConditions() => PsuPlugin.Configuration.SetupComplete;

    private void DrawStatusSection()
    {
        var service = PsuPlugin.SyncThingService;

        UIHelpers.DrawAPIStatus();

        ImGui.Text($"Stars: {service.Stars.Count}");
        
        if (service.LastRefresh != DateTime.MinValue)
        {
            var timeSince = DateTime.Now - service.LastRefresh;
            ImGui.Text($"Last refresh: {timeSince.TotalMinutes:F1} minutes ago");
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Refresh Now") && service.IsHealthy)
        {
            service.InvalidateCaches();
        }

        var myStarCode = Base64Util.ToBase64(PsuPlugin.Configuration.MyStarPack!);
        ImGui.Text("Your Star Code:");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), myStarCode);
        if (ImGui.Button("Copy to Clipboard"))
        {
            ImGui.SetClipboardText(myStarCode);
            Svc.Chat.Print("Copied Star Code to clipboard.");
        }

        if (ImGui.Button("Import Star Code from Clipboard"))
        {
            var code = ImGui.GetClipboardText();
            var starPack = Base64Util.FromBase64<StarPack>(code);
            if (starPack != null)
            {
                PsuPlugin.Configuration.StarPacks.Add(starPack);
                EzConfig.Save();
                Svc.Chat.Print("Star Code imported.");
            }
            else
            {
                Svc.Chat.PrintError("Invalid Star Code.");
            }
        }
    }
}
