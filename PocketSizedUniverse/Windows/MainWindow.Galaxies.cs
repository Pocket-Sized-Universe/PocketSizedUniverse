using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using LibGit2Sharp;
using Lumina.Excel.Sheets;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Table;
using OtterGui.Text;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Models.Data;
using PocketSizedUniverse.Windows.ViewModels;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{

    private class MembersTable : Table<StarPack>
    {
        private static readonly StarIdColumn _starIdColumn = new() {Label = "Star ID"};
        private static readonly DataPackIdColumn _dataPackIdColumn = new() {Label = "DataPack ID"};
        private static readonly ActionsColumn _actionsColumn = new() {Label = "Actions"};
        private static readonly OnlineColumn _onlineColumn = new() {Label = "Online"};
        private static readonly PlayerNameColumn _playerNameColumn = new() {Label = "Player Name"};
        public MembersTable(Galaxy galaxy, bool hasWriteAccess) : base("Members Table", galaxy.GetMembers().ToList(), _onlineColumn, _playerNameColumn, _starIdColumn, _dataPackIdColumn, _actionsColumn)
        {
            _hasWriteAccess = hasWriteAccess;
            _currentGalaxy = galaxy;
            Flags = ImGuiTableFlags.RowBg
                    | ImGuiTableFlags.Sortable
                    | ImGuiTableFlags.BordersOuter
                    | ImGuiTableFlags.ScrollY
                    | ImGuiTableFlags.ScrollX
                    | ImGuiTableFlags.PreciseWidths
                    | ImGuiTableFlags.BordersInnerV
                    | ImGuiTableFlags.SizingStretchProp
                    | ImGuiTableFlags.NoBordersInBodyUntilResize
                    | ImGuiTableFlags.Resizable;
        }

        private static bool _hasWriteAccess;
        private static Galaxy _currentGalaxy;

        public class StarIdColumn : ColumnString<StarPack>
        {
            public override float Width => ImGui.CalcTextSize(Label).X;

            public override string ToName(StarPack item)
            {
                return item.StarId[..8] + "...";
            }

            public override void DrawColumn(StarPack item, int _)
            {
                base.DrawColumn(item, _);
                ImGuiUtil.HoverTooltip(item.StarId);           
            }

            public override bool FilterFunc(StarPack item)
            {
                var id = item.StarId;
                var filter = FilterValue;
                return string.IsNullOrEmpty(filter) || id.Contains(filter, StringComparison.OrdinalIgnoreCase);
            }
        }

        public class PlayerNameColumn : ColumnString<StarPack>
        {
            public override float Width => ImGui.CalcTextSize(Label).X;

            public override string ToName(StarPack item)
            {
                if (item.StarId == PsuPlugin.Configuration.MyStarPack!.StarId)
                {
                    var localPlayer = PsuPlugin.PlayerDataService.LocalPlayerData?.Player;
                    if (localPlayer == null)
                        return "You";
                    return localPlayer.Name.TextValue + "@" + localPlayer.HomeWorld.Value.Name.ExtractText();
                }

                if (PsuPlugin.PlayerDataService.RemotePlayerData.TryGetValue(item.StarId, out var playerData))
                {
                    var player = playerData.Player;
                    if (player == null)
                        return "Unknown";
                    return player.Name.TextValue + "@" + player.HomeWorld.Value.Name.ExtractText();
                }
                
                return "Unknown";
            }
            
            public override bool FilterFunc(StarPack item)
            {
                var name = ToName(item);
                var filter = FilterValue;
                return string.IsNullOrEmpty(filter) || name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            }
        }
        
        public class DataPackIdColumn : ColumnString<StarPack>
        {
            public override float Width => ImGui.CalcTextSize(Guid.Empty.ToString()).X + 10;

            public override string ToName(StarPack item)
            {
                return item.DataPackId.ToString();
            }
            
            public override bool FilterFunc(StarPack item)
            {
                var id = item.DataPackId;
                var filter = FilterValue;
                return string.IsNullOrEmpty(filter) || id.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase);
            }
        }
        
        public class ActionsColumn : Column<StarPack>
        {
            public override float Width => ImGui.CalcTextSize(Label).X;
            public override void DrawColumn(StarPack item, int idx)
            {
                var isMe = item.StarId == PsuPlugin.Configuration.MyStarPack!.StarId;
                ImGui.BeginDisabled(!_hasWriteAccess || isMe);
                if (ImUtf8.IconButton(FontAwesomeIcon.TrashAlt))
                {
                    if (_currentGalaxy.TryRemoveMember(item))
                    {
                        Notify.Info("Removed member.");
                    }
                    else
                    {
                        Notify.Error("Failed to remove member.");
                    }
                }
                ImGui.EndDisabled();
                ImGuiUtil.HoverTooltip("Remove this member from the Galaxy.");
                ImGui.SameLine();
                ImGui.BeginDisabled(isMe);
                if (ImUtf8.IconButton(FontAwesomeIcon.Ban))
                {
                    try
                    {
                        PsuPlugin.Configuration.Blocklist.Add(item);
                        EzConfig.Save();
                        Notify.Info("Added to blocklist.");
                    }
                    catch (Exception ex)
                    {
                        Svc.Log.Error("Failed to add to blocklist: " + ex);
                        Notify.Error("Failed to add to blocklist.");
                    }
                }
                ImGui.EndDisabled();
                ImGuiUtil.HoverTooltip("Add this member to your blocklist.");
            }
        }

        public class OnlineColumn : YesNoColumn<StarPack>
        {
            public override float Width => ImGui.CalcTextSize(Label).X;

            protected override bool GetValue(StarPack item)
            {
                return item.StarId == PsuPlugin.Configuration.MyStarPack!.StarId || PsuPlugin.SyncThingService.IsStarOnline(item.StarId);
            }
        }
    }
    private GalaxySelector? _galaxySelector;

    private class GalaxySelector(IList<Galaxy> items) : ItemSelector<Galaxy>(items, Flags.Delete | Flags.Filter)
    {
        private string _joinGalaxyUrl = string.Empty;

        public new void Draw(float width)
        {
            using var id = ImRaii.PushId("GalaxySelector-Outer");
            using var group = ImRaii.Group();
            if (ImGui.Button("Join Galaxy", new Vector2(width - 5, 0)))
            {
                ImGui.OpenPopup("Join Galaxy");
            }

            if (ImGui.BeginPopup("Join Galaxy", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.InputText("Galaxy URL", ref _joinGalaxyUrl);
                if (ImGui.Button("Join"))
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                            Repository.Clone(_joinGalaxyUrl, tempPath);
                            string galaxyName;
                            using (var tempGalaxy = new Galaxy(tempPath))
                            {
                                galaxyName = tempGalaxy.Name;
                            }
                            var galaxiesDir = Path.Combine(PsuPlugin.Configuration.DefaultDataPackDirectory!, "Galaxies");
                            if (!Directory.Exists(galaxiesDir))
                                Directory.CreateDirectory(galaxiesDir);
                            var galaxyPath = Path.Combine(galaxiesDir, galaxyName);
                            if (Directory.Exists(galaxyPath))
                                galaxyPath += $"-{Guid.NewGuid().ToString().Take(6)}";
                            Repository.Clone(_joinGalaxyUrl, galaxyPath);
                            var realGalaxy = new Galaxy(galaxyPath);
                            PsuPlugin.Configuration.Galaxies.Add(realGalaxy);
                            EzConfig.Save();
                            Notify.Success("Galaxy joined!");
                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Error("Failed to join Galaxy: " + ex);
                            Notify.Error($"Failed to join Galaxy");
                        }
                    });
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (ImGui.Button("Create Galaxy", new Vector2(width - 5, 0)))
            {
                PsuPlugin.CreateGalaxyWindow.Open();
            }

            ImGui.Separator();
            base.Draw(width);
        }

        protected override bool OnDraw(int idx)
        {
            var galaxyPack = Items[idx];
            return ImGui.Selectable(galaxyPack.Name + "##" + idx, CurrentIdx == idx);
        }

        protected override bool Filtered(int idx)
        {
            if (string.IsNullOrEmpty(Filter))
                return false;
            var label = Items[idx].Name;
            var description = Items[idx].Description;
            return !label.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
                   !description.Contains(Filter, StringComparison.OrdinalIgnoreCase);
        }

        protected override bool OnDelete(int idx)
        {
            if (idx < 0 || idx >= Items.Count)
                return false;

            var galaxy = Items[idx];
            galaxy.Dispose();
            PsuPlugin.Configuration.Galaxies.Remove(galaxy);
            EzConfig.Save();
            Notify.Info("Galaxy removed.");
            return true;
        }
    }

    private void DrawGalaxies()
    {
        var galaxyPacks = PsuPlugin.Configuration.Galaxies;
        _galaxySelector ??= new GalaxySelector(galaxyPacks);

        if (ImGui.BeginChild("GalaxiesContent"))
        {
            if (ImGui.BeginChild("GalaxyList", new Vector2(350, -1), true))
            {
                _galaxySelector.Draw(340);
                ImGui.EndChild();
            }

            ImGui.SameLine();

            if (ImGui.BeginChild("GalaxyDetails", new Vector2(0, -1), true))
            {
                DrawGalaxyDetails();
                ImGui.EndChild();
            }

            ImGui.EndChild();
        }
    }

    private string _editNameString = string.Empty;
    private string _editDescriptionString = string.Empty;
    private string _repoName = string.Empty;
    private Task? _repoCreateTask;
    private Task? _repoPushTask;
    private Task? _repoPullTask;
    private Task? _repoCommitTask;
    private Task<bool>? _hasWriteAccessTask;
    private int _previousGalaxyIndex = -1;

    private void DrawGalaxyDetails()
    {
        var selectedGalaxy = _galaxySelector?.Current;
        if (selectedGalaxy == null)
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Select or create a Galaxy to view details");
            return;
        }

        if (_previousGalaxyIndex != _galaxySelector!.CurrentIdx)
        {
            _previousGalaxyIndex = _galaxySelector!.CurrentIdx;
            _hasWriteAccessTask = Task.Run(selectedGalaxy.HasWriteAccess);
        }

        var hasWriteAccess = selectedGalaxy.GetOrigin() != null && _hasWriteAccessTask is { IsCompletedSuccessfully: true, Result: true };
        ImGui.Text("Commands:");
        if (ImGuiUtil.DrawDisabledButton("Fetch Changes", new Vector2(0, 0),
                "Fetch and merge changes from remote repository.",
                _repoPullTask != null && !_repoPullTask.IsCompleted))
        {
            _repoPullTask = Task.Run(selectedGalaxy.TryFetch);
            _hasWriteAccessTask = Task.Run(selectedGalaxy.HasWriteAccess);
        }

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("Save Changes", new Vector2(0, 0), "Commit changes to local repository.",
                (_repoCommitTask != null && !_repoCommitTask.IsCompleted) || !hasWriteAccess))
        {
            _repoCommitTask = Task.Run(() => selectedGalaxy.TryCommit("Made some changes"));
        }

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("Submit Changes", new Vector2(0, 0), "Push changes to remote repository.",
                (_repoPushTask != null && !_repoPushTask.IsCompleted) || !hasWriteAccess || selectedGalaxy.GetOrigin() == null))
        {
            _repoPushTask = Task.Run(selectedGalaxy.TryPush);
        }

        if (selectedGalaxy.GetOrigin() == null)
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, "This Galaxy has not been fully initialized yet.");
            ImGui.TextColored(ImGuiColors.DalamudYellow, "You must set up a remote origin in the Settings tab before this Galaxy can be used.");
        }
        else if (!hasWriteAccess)
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, "You do not have permissions to edit this Galaxy.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.BeginTabBar("GalaxyTabs"))
        {
            if (ImGui.BeginTabItem("Info"))
            {
                DrawInfoTab(selectedGalaxy, hasWriteAccess);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Members"))
            {
                DrawMembersTab(selectedGalaxy, hasWriteAccess);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Settings"))
            {
                DrawSettingsTab(selectedGalaxy, hasWriteAccess);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawSettingsTab(Galaxy selectedGalaxy, bool hasWriteAccess)
    {
        ImGui.BeginDisabled(!hasWriteAccess && selectedGalaxy.GetOrigin() != null);
        if (ImGuiUtil.GenericEnumCombo("Remote Origin Type", 100f, selectedGalaxy.OriginType, out var newOriginType))
        {
            selectedGalaxy.OriginType = newOriginType;
            EzConfig.Save();
        }

        ImGui.EndDisabled();
        ImGui.SameLine();
        ImUtf8.Icon(FontAwesomeIcon.QuestionCircle);
        ImGuiUtil.HoverTooltip(
            "Since a Galaxy is a specialized git repository, it must be hosted on a remote server to be used by other people.\nThis setting determines how the Galaxy is accessed.\nYou can choose to use a custom git server, or use GitHub and let PSU manage your Galaxy for you.");
        ImGui.Spacing();
        if (selectedGalaxy.OriginType == GalaxyOriginType.GitHub && PsuPlugin.Configuration.GitHubToken == null &&
            selectedGalaxy.GetOrigin() == null)
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, "You must be logged into GitHub to use this option.");
            if (ImGui.Button("Authenticate with GitHub"))
            {
                PsuPlugin.GitHubLoginWindow.IsOpen = true;
            }
        }
        else if (selectedGalaxy.OriginType == GalaxyOriginType.GitHub && selectedGalaxy.GetOrigin() == null)
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, "This repository has not been initialized on GitHub yet.");
            ImGui.InputText("GitHub Repository Name", ref _repoName);
            if (_repoCreateTask != null && !_repoCreateTask.IsCompleted)
            {
                ImGui.Text("Creating repository...");
            }
            else
            {
                if (ImGui.Button("Initialize on GitHub"))
                {
                    if (string.IsNullOrEmpty(_repoName))
                    {
                        Notify.Error("You must specify a repository name.");
                        return;
                    }

                    _repoCreateTask = Task.Run(async () =>
                    {
                        var origin = await PsuPlugin.GitHubService.CreateRepository(_repoName);
                        if (origin == null)
                        {
                            Notify.Error("Failed to create repository.");
                            return;
                        }

                        selectedGalaxy.SetOrigin(origin);
                        Notify.Info("Repository created.");
                    });
                }
            }
        }
        else if (selectedGalaxy.GetOrigin() != null || selectedGalaxy.OriginType == GalaxyOriginType.Custom)
        {
            ImGui.Text($"Origin: {selectedGalaxy.GetOrigin()?.Url}");
            if (ImGui.Button("Copy to Clipboard"))
            {
                ImGui.SetClipboardText(selectedGalaxy.GetOrigin()!.Url);
            }
            ImGui.SameLine();
            if (ImGui.Button("Edit Origin"))
            {
                ImGui.OpenPopup("Edit String");
            }
            if (ImGui.BeginPopup("Edit String"))
            {
                ImGui.SetNextItemWidth(400);
                ImGui.InputText("##EditString", ref _editOriginString, 256);
                if (ImGui.Button("Save"))
                {
                    selectedGalaxy.SetOrigin(_editOriginString);
                    _hasWriteAccessTask = Task.Run(selectedGalaxy.HasWriteAccess);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }
        
        //Sync Permissions
        if (ImGui.CollapsingHeader("Sync Permissions", ImGuiTreeNodeFlags.None))
        {
            ImGui.Text("Changes to this section require 'Force Apply Data' to be clicked to take effect.");
            ImGui.Spacing();
            var permissions = selectedGalaxy.Permissions;
            var audio = permissions.HasFlag(SyncPermissions.Sounds);
            var vfx = permissions.HasFlag(SyncPermissions.Visuals);
            var animations = permissions.HasFlag(SyncPermissions.Animations);
            if (ImGui.Checkbox("Enable Sound", ref audio))
            {
                selectedGalaxy.Permissions = audio ? permissions | SyncPermissions.Sounds : permissions & ~SyncPermissions.Sounds;
                EzConfig.Save();
            }

            if (ImGui.Checkbox("Enable VFX", ref vfx))
            {
                selectedGalaxy.Permissions = vfx ? permissions | SyncPermissions.Visuals : permissions & ~SyncPermissions.Visuals;
                EzConfig.Save();
            }
            
            if (ImGui.Checkbox("Enable Animations", ref animations))
            {
                selectedGalaxy.Permissions = animations ? permissions | SyncPermissions.Animations : permissions & ~SyncPermissions.Animations;
                EzConfig.Save();
            }

            if (!permissions.HasFlag(SyncPermissions.All))
            {
                ImGui.TextColored(ImGuiColors.DalamudRed, "Some permissions are disabled. Syncing may not work as expected.");
            }
        }
    }
    
    private string _editOriginString = string.Empty;

    private void DrawMembersTab(Galaxy selectedGalaxy, bool hasWriteAccess)
    {
        ImGui.Spacing();
        ImGui.BeginDisabled(!hasWriteAccess);
        if (ImGui.Button("Add Existing Star Pair"))
        {
            ImGui.OpenPopup("Add Star Code");
        }

        if (ImGui.BeginPopup("Add Star Code"))
        {
            var starPacks = PsuPlugin.Configuration.StarPacks;
            foreach (var starPack in starPacks)
            {
                var star = starPack.GetStar();
                if (star == null)
                    continue;
                if (ImGui.Selectable(star.Name, false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    selectedGalaxy.TryAddMember(starPack);
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Add Star Code From Clipboard"))
        {
            var clip = ImGui.GetClipboardText();
            var starPack = Base64Util.FromBase64<StarPack>(clip);
            if (starPack == null)
            {
                Notify.Error("Invalid Star Code!");
            }
            else
            {
                selectedGalaxy.TryAddMember(starPack);
                Notify.Info("Added Star Code!");
            }
        }
        ImGui.EndDisabled();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        var membersTable = new MembersTable(selectedGalaxy, hasWriteAccess);
        membersTable.Draw(20);
    }

    private void DrawInfoTab(Galaxy selectedGalaxy, bool hasWriteAccess)
    {
        ImGui.BeginChild("GalaxyName", new Vector2(-1, 80), true);
        ImGui.PushID("GalaxyName");
        ImGui.Text("Galaxy Name");
        ImGui.SameLine();
        ImGui.BeginDisabled(!hasWriteAccess);
        if (ImUtf8.IconButton(FontAwesomeIcon.Clipboard))
        {
            _editNameString = selectedGalaxy.Name;
            ImGui.OpenPopup("Edit String");
        }
        ImGui.EndDisabled();
        if (ImGui.BeginPopup("Edit String"))
        {
            ImGui.SetNextItemWidth(400);
            ImGui.InputText("##EditString", ref _editNameString, 32);
            if (ImGui.Button("Save"))
            {
                selectedGalaxy.Name = _editNameString;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text(selectedGalaxy.Name);
        ImGui.PopID();
        ImGui.EndChild();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.BeginChild("GalaxyDesc", new Vector2(-1, -1), true);
        ImGui.PushID("GalaxyDescription");
        ImGui.Text("Galaxy Description");
        ImGui.SameLine();
        ImGui.BeginDisabled(!hasWriteAccess);
        if (ImUtf8.IconButton(FontAwesomeIcon.Clipboard))
        {
            _editDescriptionString = selectedGalaxy.Description;
            ImGui.OpenPopup("Edit String");
        }

        ImGui.EndDisabled();
        if (ImGui.BeginPopup("Edit String"))
        {
            ImGui.SetNextItemWidth(400);
            ImGui.InputTextMultiline("##EditString", ref _editDescriptionString, 1024, new Vector2(400, 100));
            if (ImGui.Button("Save"))
            {
                selectedGalaxy.Description = _editDescriptionString;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextWrapped(selectedGalaxy.Description);
        ImGui.PopID();
        ImGui.EndChild();
    }
}