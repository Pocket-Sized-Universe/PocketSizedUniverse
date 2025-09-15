using Syncthing.Models.Response;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility.Raii;
using ImRaii = OtterGui.Raii.ImRaii;

namespace PocketSizedUniverse.Windows.ViewModels;

public static class UIHelpers
{
    public static void DrawAPIStatus()
    {
        // API Status
        var apiColor = PsuPlugin.SyncThingService.IsHealthy ? new Vector4(0.0f, 0.8f, 0.0f, 1.0f) : new Vector4(0.8f, 0.0f, 0.0f, 1.0f);
        var apiText = PsuPlugin.SyncThingService.IsHealthy ? "Connected" : "Disconnected";

        ImGui.PushStyleColor(ImGuiCol.Text, apiColor);
        ImGui.Text($"API: {apiText}");
        if (PsuPlugin.Configuration.UseBuiltInSyncThing && (PsuPlugin.ServerProcess?.HasExited ?? true))
        {
            ImGui.Text(
                "WARNING: Easy Mode is enabled but Pocket Sized Universe is not controlling the SyncThing server it's connected to.");
            ImGui.Text("This may cause issues with syncing or it may be perfectly fine.");
            ImGui.Text("If you experience issues, please restart your computer at your earliest convenience.");
        }
        ImGui.PopStyleColor();
    }

    public static string GetStatusText(this Star star)
    {
        if (star.Paused) return "Paused";
        return "Active";
    }

    public static Vector4 GetStatusColor(this Star star)
    {
        return star.Paused ? new Vector4(0.8f, 0.4f, 0.4f, 1.0f) : new Vector4(0.4f, 0.8f, 0.4f, 1.0f);
    }
    
    // Pending folder helpers
    public static string GetDisplayName(this PendingFolder pendingFolder)
    {
        if (pendingFolder.OfferedBy.Any())
        {
            var firstInvite = pendingFolder.OfferedBy.First().Value;
            return !string.IsNullOrEmpty(firstInvite.Label) ? firstInvite.Label : pendingFolder.FolderId;
        }
        return pendingFolder.FolderId;
    }
    
    public static string GetOfferingStarsText(this PendingFolder pendingFolder)
    {
        var starCount = pendingFolder.OfferedBy.Count;
        return starCount == 1 ? "1 star" : $"{starCount} stars";
    }
    
    public static DateTime GetLatestInviteTime(this PendingFolder pendingFolder)
    {
        if (pendingFolder.OfferedBy.Any())
        {
            return pendingFolder.OfferedBy.Values.Max(invite => invite.Time);
        }
        return DateTime.MinValue;
    }

    public static bool IsValidStarId(string starId)
    {
        if (string.IsNullOrWhiteSpace(starId)) return false;
        
        // Basic SyncThing star ID validation
        // Real star IDs are base32 encoded and have specific length
        var cleanId = starId.Replace("-", "");
        return cleanId.Length == 56 && cleanId.All(c => 
            (c >= '0' && c <= '9') || 
            (c >= 'A' && c <= 'Z') ||
            (c >= 'a' && c <= 'z'));
    }

    public static string FormatPath(string path, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(path)) return "";
        
        if (path.Length <= maxLength) return path;
        
        return "..." + path[^(maxLength - 3)..];
    }
}
