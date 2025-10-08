using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
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
        public StarPackSelector(IList<StarPack> items) : base(items, Flags.Delete | Flags.Filter)
        {
        }

        public new void Draw(float width)
        {
            using var id = ImRaii.PushId("StarPackSelector-Outer");
            using var group = ImRaii.Group();
            if (ImGui.Button("Import Star Code", new Vector2(width - 5, 0)))
            {
                try
                {
                    var data = ImGui.GetClipboardText();
                    var starPack = Base64Util.FromBase64<StarPack>(data);
                    if (starPack != null)
                    {
                        // Validate not duplicate and not self
                        if (PsuPlugin.Configuration.StarPacks.Any(sp =>
                                sp.StarId == starPack.StarId || sp.DataPackId == starPack.DataPackId))
                        {
                            Notify.Error("This Star Pair is already imported.");
                        }
                        else if (PsuPlugin.Configuration.MyStarPack != null &&
                                 (PsuPlugin.Configuration.MyStarPack.StarId == starPack.StarId ||
                                  PsuPlugin.Configuration.MyStarPack.DataPackId == starPack.DataPackId))
                        {
                            Notify.Error("You cannot import your own Star Code.");
                        }
                        else
                        {
                            PsuPlugin.Configuration.StarPacks.Add(starPack);
                            EzConfig.Save();
                            Notify.Info("Imported Star Pair successfully!");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Svc.Log.Error($"Failed to import Star Pair: {ex.Message}");
                    Notify.Error("Failed to import Star Pair. Invalid format.");
                }
            }

            ImGui.Separator();
            base.Draw(width);
        }

        protected override bool OnDraw(int idx)
        {
            var starPack = Items[idx];
            var star = starPack.GetStar();
            var dataPack = starPack.GetDataPack();

            var displayName = star?.Name ?? $"Star-{starPack.StarId[..8]}";
            var online = PsuPlugin.SyncThingService.IsStarOnline(starPack.StarId);
            var statusColor = (star != null)
                ? UIHelpers.GetOnlineColor(online, star.Paused)
                : new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            var statusIcon = star?.Paused == true ? FontAwesomeIcon.Pause : FontAwesomeIcon.Star;
            var rates = PsuPlugin.SyncThingService.GetTransferRates(starPack.StarId);
            bool syncing = rates != null && (rates.InBps > 100 || rates.OutBps > 100);

            var isSelected = CurrentIdx == idx;

            // ItemSelector.InternalDraw adds FramePadding.X to cursor position, 
            // so we need to move back to get the full width
            var currentPosX = ImGui.GetCursorPosX();
            var originalPosX = currentPosX - ImGui.GetStyle().FramePadding.X;
            ImGui.SetCursorPosX(originalPosX);

            // Get available width from the original position
            var availableWidth = ImGui.GetContentRegionAvail().X;

            // Create a single selectable that covers the entire available width
            var result = ImGui.Selectable($"##{idx}", isSelected, ImGuiSelectableFlags.None,
                new Vector2(availableWidth, 60));

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
            var dpName = dataPack?.Name ?? "Unknown";
            if (syncing)
                dpName += " (Sync In Progress)";
            ImGui.Text($"  DataPack: {dpName}");
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
        ImGui.Separator();
        
        //Sync Permissions
        if (ImGui.CollapsingHeader("Sync Permissions", ImGuiTreeNodeFlags.None))
        {
            ImGui.Text("Changes to this section require 'Force Apply Data' to be clicked to take effect.");
            ImGui.Spacing();
            var permissions = selectedStarPack.SyncPermissions;
            var audio = permissions.HasFlag(SyncPermissions.Sounds);
            var vfx = permissions.HasFlag(SyncPermissions.Visuals);
            var animations = permissions.HasFlag(SyncPermissions.Animations);
            if (ImGui.Checkbox("Enable Sound", ref audio))
            {
                selectedStarPack.SyncPermissions = audio ? permissions | SyncPermissions.Sounds : permissions & ~SyncPermissions.Sounds;
                EzConfig.Save();
            }

            if (ImGui.Checkbox("Enable VFX", ref vfx))
            {
                selectedStarPack.SyncPermissions = vfx ? permissions | SyncPermissions.Visuals : permissions & ~SyncPermissions.Visuals;
                EzConfig.Save();
            }
            
            if (ImGui.Checkbox("Enable Animations", ref animations))
            {
                selectedStarPack.SyncPermissions = animations ? permissions | SyncPermissions.Animations : permissions & ~SyncPermissions.Animations;
                EzConfig.Save();
            }

            if (!permissions.HasFlag(SyncPermissions.All))
            {
                ImGui.TextColored(ImGuiColors.DalamudRed, "Some permissions are disabled. Syncing may not work as expected.");
            }
        }

        ImGui.Spacing();
        ImGui.Separator();

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
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("No Changes", new Vector2(120, 0));
            ImGui.EndDisabled();
        }
    }

// Common Pairs removed

    private void DrawTransferStatus(StarPack selectedStarPack)
    {
        var star = selectedStarPack.GetStar();
        var dataPack = selectedStarPack.GetDataPack();

        if (star == null || dataPack == null)
        {
            ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.4f, 1.0f), "Unable to retrieve status information.");
            return;
        }

        var online = PsuPlugin.SyncThingService.IsStarOnline(selectedStarPack.StarId);
        var statusColor = UIHelpers.GetOnlineColor(online, star.Paused);
        var statusText = UIHelpers.GetOnlineText(online, star.Paused);
        ImGui.TextColored(statusColor, $"Connection: {statusText}");

        // DataPack info
        ImGui.Text($"Path: {UIHelpers.FormatPath(dataPack.Path ?? "", 40)}");

        // Live transfer details
        var rates = PsuPlugin.SyncThingService.GetTransferRates(selectedStarPack.StarId);
        var conn = PsuPlugin.SyncThingService.Connections?.Connections.GetValueOrDefault(selectedStarPack.StarId);
        if (rates != null && conn != null)
        {
            ImGui.Spacing();
            ImGui.Text($"Address: {conn.Address}");
            ImGui.Text($"Type: {conn.Type}{(conn.IsLocal ? " (local)" : string.Empty)}");
            ImGui.Text(
                $"Up: {UIHelpers.FormatRate(rates.OutBps)}  (Total {UIHelpers.FormatBytes(rates.OutBytesTotal)})");
            ImGui.Text(
                $"Down: {UIHelpers.FormatRate(rates.InBps)}  (Total {UIHelpers.FormatBytes(rates.InBytesTotal)})");
            if (conn.StartedAt != default)
                ImGui.Text($"Connected Since: {conn.StartedAt:G}");
            ImGui.Text($"Client: {conn.ClientVersion}");
        }
        else
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "No live transfer data available yet.");
        }
    }
}