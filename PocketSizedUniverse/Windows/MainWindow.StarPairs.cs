using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Models.Data;
using OtterGui;
using OtterGui.Raii;
using PocketSizedUniverse.Windows.ViewModels;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{
    private StarPackSelector? _starPackSelector;
    
    private class StarPackSelector : ItemSelector<StarPack>
    {
        public StarPackSelector(IList<StarPack> items) : base(items, Flags.Import | Flags.Delete | Flags.Filter)
        {
        }

        protected override bool OnDraw(int idx)
        {
            var starPack = Items[idx];
            var star = starPack.GetStar();
            var dataPack = starPack.GetDataPack();
            
            var displayName = star?.Name ?? $"Star-{starPack.StarId[..8]}";
            var statusColor = star?.GetStatusColor() ?? new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            var statusIcon = star?.Paused == true ? FontAwesomeIcon.Pause : FontAwesomeIcon.Star;
            
            var isSelected = CurrentIdx == idx;
            
            // ItemSelector.InternalDraw adds FramePadding.X to cursor position, 
            // so we need to move back to get the full width
            var currentPosX = ImGui.GetCursorPosX();
            var originalPosX = currentPosX - ImGui.GetStyle().FramePadding.X;
            ImGui.SetCursorPosX(originalPosX);
            
            // Get available width from the original position
            var availableWidth = ImGui.GetContentRegionAvail().X;
            
            // Create a single selectable that covers the entire available width
            var result = ImGui.Selectable($"##{idx}", isSelected, ImGuiSelectableFlags.None, new Vector2(availableWidth, 60));
            
            // Draw content within the selectable area
            // Get the cursor position right after the selectable 
            var cursorPos = ImGui.GetCursorPos();
            // Move cursor back up to draw over the selectable
            ImGui.SetCursorPos(new Vector2(originalPosX + 4, cursorPos.Y - 60));
            
            // Draw the icon and main text
            ImGui.PushStyleColor(ImGuiCol.Text, statusColor);
            ImGuiUtil.PrintIcon(statusIcon);
            ImGui.SameLine();
            ImGui.Text(displayName);
            ImGui.PopStyleColor();
            
            // Draw additional info on new lines
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
            ImGui.Text($"  DataPack: {dataPack?.Name ?? "Unknown"}");
            ImGui.Text($"  ID: {starPack.StarId[..8]}...");
            ImGui.PopStyleColor();
            
            // Reset cursor to the original adjusted position for next item
            ImGui.SetCursorPos(new Vector2(currentPosX, cursorPos.Y));
            
            return result;
        }

        protected override bool Filtered(int idx)
        {
            if (string.IsNullOrEmpty(Filter))
                return false;
                
            var starPack = Items[idx];
            var star = starPack.GetStar();
            var dataPack = starPack.GetDataPack();
            
            var displayName = star?.Name ?? $"Star-{starPack.StarId[..8]}";
            var dataPackName = dataPack?.Name ?? "Unknown";
            
            return !displayName.Contains(Filter, StringComparison.OrdinalIgnoreCase) &&
                   !dataPackName.Contains(Filter, StringComparison.OrdinalIgnoreCase) &&
                   !starPack.StarId.Contains(Filter, StringComparison.OrdinalIgnoreCase);
        }

        protected override bool OnDelete(int idx)
        {
            if (idx < 0 || idx >= Items.Count)
                return false;
                
            var starPack = Items[idx];
            PsuPlugin.Configuration.StarPacks.Remove(starPack);
            EzConfig.Save();
            Notify.Info("Star Pair deleted.");
            return true;
        }

        protected override bool OnClipboardImport(string name, string data)
        {
            try
            {
                var starPack = Base64Util.FromBase64<StarPack>(data);
                if (starPack != null)
                {
                    // Check if this StarPack already exists
                    if (PsuPlugin.Configuration.StarPacks.Any(sp => sp.StarId == starPack.StarId))
                    {
                    Notify.Error("This Star Pair is already imported.");
                        return false;
                    }
                    
                    PsuPlugin.Configuration.StarPacks.Add(starPack);
                    EzConfig.Save();
                Notify.Info("Imported Star Pair successfully!");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Failed to import Star Pair: {ex.Message}");
                Notify.Error("Failed to import Star Pair. Invalid format.");
            }
            return false;
        }
    }

    private void DrawStarPairs()
    {
        var starPacks = PsuPlugin.Configuration.StarPacks;
        
        // Initialize the selector if not done yet or if the collection changed
        _starPackSelector ??= new StarPackSelector(starPacks);

        // Two-panel layout using ItemSelector
        if (ImGui.BeginChild("StarPairsContent", new Vector2(-1, -1)))
        {
            // Left panel - ItemSelector
            if (ImGui.BeginChild("StarPackList", new Vector2(350, -1), true))
            {
                _starPackSelector.Draw(340);
                ImGui.EndChild();
            }

            ImGui.SameLine();

            // Right panel - Selected StarPack details  
            if (ImGui.BeginChild("StarPackDetails", new Vector2(-1, -1), true))
            {
                DrawSelectedStarPackDetails();
                ImGui.EndChild();
            }
            
            ImGui.EndChild();
        }
    }


    private void DrawSelectedStarPackDetails()
    {
        var selectedStarPack = _starPackSelector?.Current;
        if (selectedStarPack == null)
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Select a Star Pair to view details");
            return;
        }

        var star = selectedStarPack.GetStar();
        var dataPack = selectedStarPack.GetDataPack();

        // Header
        var displayName = star?.Name ?? $"Star-{selectedStarPack.StarId[..8]}";
        ImGui.Text($"Editing: {displayName}");
        ImGui.Separator();
        ImGui.Spacing();

        // Star settings
        if (ImGui.CollapsingHeader("Star Settings", ImGuiTreeNodeFlags.None))
        {
            DrawStarEditor(star);
        }

        ImGui.Spacing();
        
        // DataPack settings
        if (ImGui.CollapsingHeader("DataPack Settings", ImGuiTreeNodeFlags.None))
        {
            DrawDataPackEditor(dataPack);
        }

        ImGui.Spacing();
        
        // Common pairs section
        if (ImGui.CollapsingHeader("Common Pairs", ImGuiTreeNodeFlags.None))
        {
            DrawCommonPairs(selectedStarPack);
        }

        ImGui.Spacing();
        
        // Status section
        if (ImGui.CollapsingHeader("Transfer Status", ImGuiTreeNodeFlags.None))
        {
            DrawTransferStatus(selectedStarPack);
        }

        ImGui.Spacing();
        ImGui.Separator();

        // Save button
        if (_starEditorChanged || _dataPackEditorChanged)
        {
            if (ImGui.Button("Save Changes", new Vector2(120, 0)))
            {
                SaveChanges();
                Notify.Info("Changes saved successfully!");
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Discard Changes", new Vector2(120, 0)))
            {
                PsuPlugin.SyncThingService.InvalidateCaches();
                _starEditorChanged = false;
                _dataPackEditorChanged = false;
            }
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("No Changes", new Vector2(120, 0));
            ImGui.EndDisabled();
        }
    }

    private void DrawCommonPairs(StarPack selectedStarPack)
    {
        // Find other StarPacks that share the same DataPack ID
        var commonPairs = PsuPlugin.Configuration.StarPacks
            .Where(sp => sp.DataPackId == selectedStarPack.DataPackId && sp != selectedStarPack)
            .ToList();

        if (commonPairs.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "No common pairs found.");
            return;
        }

        ImGui.Text($"Found {commonPairs.Count} star(s) sharing this DataPack:");
        ImGui.Spacing();

        foreach (var pair in commonPairs)
        {
            var otherStar = pair.GetStar();
            var statusColor = otherStar?.GetStatusColor() ?? new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            var statusIcon = otherStar?.Paused == true ? FontAwesomeIcon.Pause : FontAwesomeIcon.Star;
            
            ImGuiUtil.PrintIcon(statusIcon);
            ImGui.SameLine();
            ImGui.TextColored(statusColor, otherStar?.Name ?? $"Star-{pair.StarId[..8]}");
        }
    }

    private void DrawTransferStatus(StarPack selectedStarPack)
    {
        var star = selectedStarPack.GetStar();
        var dataPack = selectedStarPack.GetDataPack();

        if (star == null || dataPack == null)
        {
            ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.4f, 1.0f), "Unable to retrieve status information.");
            return;
        }

        // Connection status
        var statusColor = star.GetStatusColor();
        var statusText = star.GetStatusText();
        ImGui.TextColored(statusColor, $"Connection: {statusText}");

        // DataPack info
        ImGui.Text($"Type: {dataPack.GetTypeText()}");
        ImGui.Text($"Path: {UIHelpers.FormatPath(dataPack.Path ?? "", 40)}");
        ImGui.Text($"Shared with: {dataPack.GetStarCount()} star(s)");

        // Additional stats could be added here when available from the SyncThing API
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Detailed transfer statistics coming soon...");
    }

}
