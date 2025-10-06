using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Style;
using ECommons.ImGuiMethods;
using PocketSizedUniverse.Windows.ViewModels;
using OtterGui;
using PocketSizedUniverse.Models.Data;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{
    private void DrawTopStatus()
    {
        var service = PsuPlugin.SyncThingService;

        // API Connection Status
        UIHelpers.DrawAPIStatus();

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        // Star Count
        ImGuiUtil.PrintIcon(FontAwesomeIcon.Star);
        ImGui.SameLine();
        ImGui.Text($"Stars: {service.Stars.Count}");

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        // DataPack Count  
        ImGuiUtil.PrintIcon(FontAwesomeIcon.FolderOpen);
        ImGui.SameLine();
        ImGui.Text($"DataPacks: {service.DataPacks.Count}");

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        // Last Refresh Time
        if (service.LastRefresh != DateTime.MinValue)
        {
            ImGui.Text($"Last refresh: {service.LastRefresh:HH:mm:ss}");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Never refreshed");
        }

        ImGui.SameLine();

        // Refresh Button
        if (ImGui.Button("Refresh Now") && service.IsHealthy)
        {
            service.InvalidateCaches();
        }

        if (!service.IsHealthy)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.4f, 1.0f), "(Service unavailable)");
        }
        else
        {
            ImGui.SameLine();
            if (ImGui.Button("Copy My Star Code"))
            {
                var starCode = PsuPlugin.Configuration.MyStarPack;
                if (starCode == null)
                    Notify.Error("No star code available.");
                else
                {
                    var export = Base64Util.ToBase64(starCode);
                    ImGui.SetClipboardText(export);
                    Notify.Info("Copied Star Code to clipboard.");
                }
            }
        }

        if (PsuPlugin.Configuration.UseBuiltInSyncThing && (PsuPlugin.ServerProcess?.HasExited ?? true))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
            ImGui.Text(
                "WARNING: Easy Mode is enabled but Pocket Sized Universe is not controlling the SyncThing server it's connected to.");
            ImGui.Text("This may cause issues with syncing or it may be perfectly fine.");
            ImGui.Text("If you experience issues, please restart your computer at your earliest convenience.");
            ImGui.PopStyleColor();
        }
        if (!PsuPlugin.Configuration.EnableVirusScanning)
            ImGui.TextColored(ImGuiColors.DalamudYellow, "WARNING: Virus scanning is disabled. Your system may be at risk of malware. Please be extra careful who you pair with.");
    }
}