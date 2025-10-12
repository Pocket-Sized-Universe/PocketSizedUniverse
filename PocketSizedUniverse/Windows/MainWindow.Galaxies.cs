using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using LibGit2Sharp;
using OtterGui;
using OtterGui.Raii;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Windows.ViewModels;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{
    private GalaxySelector? _galaxySelector;
    private class GalaxySelector(IList<Galaxy> items) : ItemSelector<Galaxy>(items, Flags.Delete | Flags.Filter)
    {
        private bool _createGalaxyPopupOpen = false;
        private string _galaxyName = string.Empty;
        private bool _joinGalaxyPopupOpen = false;
        public new void Draw(float width)
        {
            using var id = ImRaii.PushId("GalaxySelector-Outer");
            using var group = ImRaii.Group();
            if (ImGui.Button("Import Galaxy Code", new Vector2(width - 5, 0)))
            {
                _joinGalaxyPopupOpen = true;
                ImGui.OpenPopup("Join Galaxy");
            }
            if (ImGui.Button("Create New Galaxy", new Vector2(width - 5, 0)))
            {
                _createGalaxyPopupOpen = true;
                _galaxyName = $"{Adjectives.GetRandom()} {Nouns.GetRandom()}";
                ImGui.OpenPopup("Create New Galaxy");           
            }
            ImGui.Separator();
            base.Draw(width);
            if (ImGui.BeginPopupModal("Create New Galaxy", ref _createGalaxyPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.SetWindowSize(new Vector2(400, 200));
                ImGui.InputText("Galaxy Name", ref _galaxyName, 256);
                ImGui.Spacing();
                if (ImGui.Button("Create Galaxy", new Vector2(120, 0)))
                {
                    var name = _galaxyName.Trim();
                    var basePath = Path.Combine(PsuPlugin.Configuration.DefaultDataPackDirectory!, "Galaxies");
                    if (!Directory.Exists(basePath))
                        Directory.CreateDirectory(basePath);
                    var path = Path.Combine(basePath, name);
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    Repository.Init(path);
                    var galaxy = new Galaxy(path)
                    {
                        Name = name,
                        Description = "My new Galaxy!"
                    };
                    galaxy.EnsureDirectories();
                    galaxy.TryAddMember(PsuPlugin.Configuration.MyStarPack!);
                    PsuPlugin.Configuration.Galaxies.Add(galaxy);
                    EzConfig.Save();
                    ImGui.CloseCurrentPopup();
                    _createGalaxyPopupOpen = false;
                    Notify.Info("Galaxy created. Please allow up to 30 seconds for data propagation.");
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                    _createGalaxyPopupOpen = false;
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("Join Galaxy", ref _joinGalaxyPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                
            }
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
            Notify.Info("Galaxy deleted.");
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
    
    private void DrawGalaxyDetails()
    {
        var selectedGalaxy = _galaxySelector?.Current;
        if (selectedGalaxy == null)
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Select or create a Galaxy to view details");
            return;
        }
        ImGui.Text($"Editing: {selectedGalaxy.Name}");
        ImGui.Separator();
        ImGui.Spacing();
    }
}
