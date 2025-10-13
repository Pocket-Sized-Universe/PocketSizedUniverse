using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using LibGit2Sharp;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Models.Data;
using PocketSizedUniverse.Windows.ViewModels;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{
    private GalaxySelector? _galaxySelector;

    private class GalaxySelector(IList<Galaxy> items) : ItemSelector<Galaxy>(items, Flags.Delete | Flags.Filter)
    {
        private string _joinGalaxyUrl = string.Empty;

        private string _joinGalaxyPath =
            Path.Combine(PsuPlugin.Configuration.DefaultDataPackDirectory!, "Galaxies", "New Galaxy");

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
                ImGui.InputText("Storage Path", ref _joinGalaxyPath);
                if (Directory.Exists(_joinGalaxyPath))
                    ImGui.TextColored(ImGuiColors.DalamudOrange, "This directory already exists.");
                if (ImGui.Button("Join"))
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            Repository.Clone(_joinGalaxyUrl, _joinGalaxyPath);
                            var galaxy = new Galaxy(_joinGalaxyPath);
                            PsuPlugin.Configuration.Galaxies.Add(galaxy);
                            EzConfig.Save();
                            Notify.Info("Galaxy joined!");
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
            return !label.Contains(Filter, StringComparison.OrdinalIgnoreCase);
        }

        protected override bool OnDelete(int idx)
        {
            if (idx < 0 || idx >= Items.Count)
                return false;

            var galaxy = Items[idx];
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
        var hasWriteAccess = _hasWriteAccessTask is { IsCompletedSuccessfully: true, Result: true };
        if (!hasWriteAccess)
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, "You do not have permissions to edit this Galaxy.");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        ImGui.PushID("GalaxyName");
        ImGui.Text($"{selectedGalaxy.Name}");
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
            ImGui.InputText("##EditString", ref _editNameString, 256);
            if (ImGui.Button("Save"))
            {
                selectedGalaxy.Name = _editNameString;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        ImGui.PopID();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.PushID("GalaxyDescription");
        ImGui.TextWrapped(selectedGalaxy.Description);
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

        ImGui.PopID();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.BeginDisabled(!hasWriteAccess);
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
        else if (selectedGalaxy.GetOrigin() != null)
        {
            ImGui.Text($"Origin: {selectedGalaxy.GetOrigin()!.Url}");
            if (ImGui.Button("Copy to Clipboard"))
            {
                ImGui.SetClipboardText(selectedGalaxy.GetOrigin()!.Url);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Text("Commands:");
            if (ImGuiUtil.DrawDisabledButton("Fetch Changes", new Vector2(0, 0),
                    "Fetch and merge changes from remote repository.",
                    _repoPullTask != null && !_repoPullTask.IsCompleted))
            {
                _repoPullTask = Task.Run(selectedGalaxy.TryFetchAndMerge);
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
                    (_repoPushTask != null && !_repoPushTask.IsCompleted) || !hasWriteAccess))
            {
                _repoPushTask = Task.Run(selectedGalaxy.TryPush);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Text("Members:");
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
            foreach (var star in selectedGalaxy.GetMembers())
            {
                var realStar = star.GetStar();
                var realDataPack = star.GetDataPack();
                if (realStar == null || realDataPack == null)
                    continue;
                ImGui.PushID(star.StarId);
                if (ImGui.CollapsingHeader(realStar.Name, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (ImGui.Button($"Remove##{realStar.StarId}"))
                    {
                        selectedGalaxy.TryRemoveMember(star);
                        Notify.Info($"Removed {realStar.Name} from Galaxy {selectedGalaxy.Name}!");
                    }
                    ImGui.Indent();
                    DrawStarEditor(realStar);
                    DrawDataPackEditor(realDataPack);
                    DrawTransferStatus(star);
                    ImGui.Unindent();
                }

                ImGui.PopID();
            }
        }
    }
}