using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Syncthing.Models.Response;
using OtterGui;
using OtterGui.Text;
using PocketSizedUniverse.Windows.ViewModels;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{
    private bool _starEditorChanged = false;
    private bool _dataPackEditorChanged = false;

    private static void SetInputWidth(int width) => ImGui.SetNextItemWidth(width);

    private bool DrawStarEditor(Star? star)
    {
        if (star == null)
        {
            ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.4f, 1.0f), "Star not found");
            return false;
        }

        bool changed = false;

        // Star name
        var tempName = star.Name ?? "";
        SetInputWidth(250);
        if (ImGui.InputText("Star Name", ref tempName, 128))
        {
            star.Name = tempName;
            changed = true;
        }

        ImGui.SameLine();
        if (ImUtf8.IconButton(FontAwesomeIcon.Recycle))
        {
            var newName = Adjectives.GetRandom() + " " + Nouns.GetRandom();
            star.Name = newName;
            changed = true;
        }

        // Show Star ID (read-only)
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), $"Star ID: {star.StarId}");

        // Status indicator
        var statusColor = star.GetStatusColor();
        var statusText = star.GetStatusText();
        ImGui.TextColored(statusColor, $"Status: {statusText}");

        ImGui.Spacing();

        // Bandwidth limits
        ImGui.Text("Bandwidth Limits (0 = unlimited):");

        var tempMaxSend = star.MaxSendKbps;
        SetInputWidth(120);
        if (ImGui.SliderInt("Max Upload (KB/s)", ref tempMaxSend, 0, 10000))
        {
            star.MaxSendKbps = tempMaxSend;
            changed = true;
        }

        var tempMaxRecv = star.MaxRecvKbps;
        SetInputWidth(120);
        if (ImGui.SliderInt("Max Download (KB/s)", ref tempMaxRecv, 0, 10000))
        {
            star.MaxRecvKbps = tempMaxRecv;
            changed = true;
        }

        ImGui.Spacing();

        // Checkboxes
        var tempPaused = star.Paused;
        if (ImGui.Checkbox("Pause synchronization", ref tempPaused))
        {
            star.Paused = tempPaused;
            changed = true;
        }

        // Compression dropdown
        ImGui.Spacing();
        var compressionTypes = new[] { "always", "metadata", "never" };
        var currentCompressionIndex = Array.IndexOf(compressionTypes, star.Compression ?? "metadata");
        if (currentCompressionIndex == -1) currentCompressionIndex = 1; // default to metadata

        SetInputWidth(150);
        if (ImGui.Combo("Compression", ref currentCompressionIndex, compressionTypes, compressionTypes.Length))
        {
            star.Compression = compressionTypes[currentCompressionIndex];
            changed = true;
        }

        if (changed)
        {
            _starEditorChanged = true;
        }

        return changed;
    }

    private bool DrawDataPackEditor(DataPack? dataPack)
    {
        if (dataPack == null)
        {
            ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.4f, 1.0f), "DataPack not found");
            return false;
        }

        bool changed = false;

        // DataPack name
        var tempName = dataPack.Name ?? "";
        SetInputWidth(250);
        if (ImGui.InputText("DataPack Name", ref tempName, 128))
        {
            dataPack.Name = tempName;
            changed = true;
        }

        ImGui.SameLine();
        if (ImUtf8.IconButton(FontAwesomeIcon.Recycle))
        {
            var newName = Adjectives.GetRandom() + " " + Nouns.GetRandom();
            dataPack.Name = newName;
            changed = true;
        }

        // Show DataPack ID (read-only)
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), $"DataPack ID: {dataPack.Id}");

        // Path display
        ImGui.Text($"Path: {UIHelpers.FormatPath(dataPack.Path ?? "", 60)}");

        ImGui.Spacing();

        // Advanced settings
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Advanced Settings", ImGuiTreeNodeFlags.None))
        {
            var tempRescanInterval = dataPack.RescanIntervalS;
            SetInputWidth(120);
            if (ImGui.InputInt("Rescan Interval (seconds)", ref tempRescanInterval))
            {
                dataPack.RescanIntervalS = Math.Max(0, tempRescanInterval);
                changed = true;
            }

            var tempFsWatcherEnabled = dataPack.FsWatcherEnabled;
            if (ImGui.Checkbox("File system watcher enabled", ref tempFsWatcherEnabled))
            {
                dataPack.FsWatcherEnabled = tempFsWatcherEnabled;
                changed = true;
            }

            if (dataPack.FsWatcherEnabled)
            {
                var tempFsWatcherDelay = dataPack.FsWatcherDelayS;
                SetInputWidth(120);
                if (ImGui.InputInt("File system watcher delay (seconds)", ref tempFsWatcherDelay))
                {
                    dataPack.FsWatcherDelayS = Math.Max(1, tempFsWatcherDelay);
                    changed = true;
                }
            }

            var tempIgnorePerms = dataPack.IgnorePerms;
            if (ImGui.Checkbox("Ignore permissions", ref tempIgnorePerms))
            {
                dataPack.IgnorePerms = tempIgnorePerms;
                changed = true;
            }

            var tempAutoNormalize = dataPack.AutoNormalize;
            if (ImGui.Checkbox("Auto normalize", ref tempAutoNormalize))
            {
                dataPack.AutoNormalize = tempAutoNormalize;
                changed = true;
            }
        }

        if (changed)
        {
            _dataPackEditorChanged = true;
        }

        return changed;
    }

    private void SaveChanges()
    {
        if (_starEditorChanged || _dataPackEditorChanged)
        {
            // Save changes through the SyncThingService
            var service = PsuPlugin.SyncThingService;

            if (_starEditorChanged)
            {
                // Post updated stars to the API
                foreach (var star in service.Stars.Values)
                {
                    _ = service.PostNewStar(star);
                }

                _starEditorChanged = false;
            }

            if (_dataPackEditorChanged)
            {
                // Update DataPacks by merging editable fields (preserve server Path and membership)
                foreach (var dataPack in service.DataPacks.Values)
                {
                    _ = service.PutDataPackMerged(dataPack);
                }

                _dataPackEditorChanged = false;
            }
        }
    }
}