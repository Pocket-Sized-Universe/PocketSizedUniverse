using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using LibGit2Sharp;
using OtterGui;
using OtterGui.Text;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Windows.ViewModels;

namespace PocketSizedUniverse.Windows;

public class CreateGalaxyWindow : Window
{
    public CreateGalaxyWindow() : base("Create Galaxy")
    {
        Size = new Vector2(400, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
        _fileDialogManager = new FileDialogManager();
        _galaxyName = $"{Adjectives.GetRandom()} {Nouns.GetRandom()}";
        _description = "My new Galaxy!";
        _localPath = Path.Combine(PsuPlugin.Configuration.DefaultDataPackDirectory!, "Galaxies", _galaxyName);
    }
    private FileDialogManager _fileDialogManager;
    private string _galaxyName;
    private string _description;
    private string _localPath;

    public void Open()
    {
        _galaxyName = $"{Adjectives.GetRandom()} {Nouns.GetRandom()}";
        _description = "My new Galaxy!";
        _localPath = Path.Combine(PsuPlugin.Configuration.DefaultDataPackDirectory!, "Galaxies", _galaxyName);
        IsOpen = true;
    }
    public override void Draw()
    {
        ImGui.InputText("Storage Path", ref _localPath);
        ImGui.SameLine();
        if (ImUtf8.IconButton(FontAwesomeIcon.Folder))
        {
            _fileDialogManager.OpenFolderDialog("Select Storage Path", (b, s) =>
            {
                if (b && !string.IsNullOrEmpty(s))
                    _localPath = s;
            });
        }
        ImGui.Spacing();
        ImGui.InputText("Galaxy Name", ref _galaxyName);
        ImGui.InputTextMultiline("Description", ref _description);
        if (ImGui.Button("Save Galaxy"))
        {
            if (!Directory.Exists(_localPath))
                Directory.CreateDirectory(_localPath);
            Repository.Init(_localPath);
            var galaxy = new Galaxy(_localPath)
            {
                Name = _galaxyName,
                Description = _description,
                OriginType = GalaxyOriginType.GitHub,
            };
            galaxy.EnsureDirectories();
            galaxy.TryAddMember(PsuPlugin.Configuration.MyStarPack!);
            galaxy.TryCommit("Initial commit");
            PsuPlugin.Configuration.Galaxies.Add(galaxy);
            EzConfig.Save();
            Notify.Info("Galaxy created!");
            IsOpen = false;
        }
        _fileDialogManager.Draw();
    }
}