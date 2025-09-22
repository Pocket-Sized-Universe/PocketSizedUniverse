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
        var apiColor = PsuPlugin.SyncThingService.IsHealthy
            ? new Vector4(0.0f, 0.8f, 0.0f, 1.0f)
            : new Vector4(0.8f, 0.0f, 0.0f, 1.0f);
        var apiText = PsuPlugin.SyncThingService.IsHealthy ? "Connected" : "Disconnected";

        ImGui.PushStyleColor(ImGuiCol.Text, apiColor);
        ImGui.Text($"API: {apiText}");
        ImGui.PopStyleColor();
    }

    public static uint Color(byte r, byte g, byte b, byte a)
    { uint ret = a; ret <<= 8; ret += b; ret <<= 8; ret += g; ret <<= 8; ret += r; return ret; }

    // Online/Offline helpers based on live connections
    public static string GetOnlineText(bool online, bool paused)
        => paused ? "Paused" : (online ? "Online" : "Offline");

    public static Vector4 GetOnlineColor(bool online, bool paused)
    {
        if (paused) return new Vector4(0.8f, 0.6f, 0.2f, 1.0f); // amber for paused
        return online ? new Vector4(0.4f, 0.8f, 0.4f, 1.0f) : new Vector4(0.8f, 0.4f, 0.4f, 1.0f);
    }

    public static string GetStatusText(this Star star)
    {
        var online = PsuPlugin.SyncThingService.IsStarOnline(star.StarId);
        return GetOnlineText(online, star.Paused);
    }

    public static Vector4 GetStatusColor(this Star star)
    {
        var online = PsuPlugin.SyncThingService.IsStarOnline(star.StarId);
        return GetOnlineColor(online, star.Paused);
    }

    public static string FormatRate(double bytesPerSecond)
    {
        var abs = Math.Abs(bytesPerSecond);
        const double k = 1024.0;
        if (abs >= k * k * k) return $"{bytesPerSecond / (k * k * k):F2} GiB/s";
        if (abs >= k * k) return $"{bytesPerSecond / (k * k):F2} MiB/s";
        if (abs >= k) return $"{bytesPerSecond / k:F2} KiB/s";
        return $"{bytesPerSecond:F0} B/s";
    }

    public static string FormatBytes(long bytes)
    {
        var abs = Math.Abs((double)bytes);
        const double k = 1024.0;
        if (abs >= k * k * k) return $"{bytes / (k * k * k):F2} GiB";
        if (abs >= k * k) return $"{bytes / (k * k):F2} MiB";
        if (abs >= k) return $"{bytes / k:F2} KiB";
        return $"{bytes} B";
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