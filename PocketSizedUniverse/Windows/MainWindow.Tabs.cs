using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{
    private void DrawTabs()
    {
        if (ImGui.BeginTabBar("MainWindowTabBar", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("Star Pairs"))
            {
                if (PsuPlugin.SyncThingService.IsHealthy)
                {
                    DrawStarPairs();
                }
                else
                {
                    DrawNotConnectedMessage();
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Galaxies"))
            {
                if (PsuPlugin.SyncThingService.IsHealthy)
                {
                    DrawGalaxies();
                }
                else
                {
                    DrawNotConnectedMessage();
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("My Star Code"))
            {
                if (PsuPlugin.SyncThingService.IsHealthy)
                {
                    DrawMyStarCode();
                }
                else
                {
                    DrawNotConnectedMessage();
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Settings"))
            {
                DrawSettings();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawNotConnectedMessage()
    {
        ImGui.TextWrapped("SyncThing service is not connected or is unhealthy. Please check your configuration in the Settings tab.");
        
        ImGui.Spacing();
        if (ImGui.Button("Refresh Connection"))
        {
            PsuPlugin.SyncThingService.InvalidateCaches();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Open Settings"))
        {
            // Switch to Settings tab - this is handled by ImGui's tab system
            // User will need to click the Settings tab manually
        }
    }
}
