using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Style;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using PocketSizedUniverse.Services;

namespace PocketSizedUniverse.GUI;

public class SetupWindow : Window
{
    private readonly Config.Configuration _configuration;
    private readonly IpfsService _ipfsService;
    public SetupWindow(Config.Configuration configuration, IpfsService ipfsService) : base("Pocket Sized Universe Setup", ImGuiWindowFlags.Modal)
    {
        _configuration = configuration;
        _ipfsService = ipfsService;
    }

    public override void Draw()
    {
        if (string.IsNullOrEmpty(_configuration.CacheDirectory))
            DrawCacheSetup();
        else if (_configuration.IpfsMode == null && !(_saveTask?.IsCompletedSuccessfully ?? false))
            DrawIpfsSetup();
        else
            DrawWelcome();
    }

    private void DrawWelcome()
    {
        ImGui.Text("Welcome to Pocket Sized Universe!");
        ImGui.Text("You may now close this window.");
        if (ImGui.Button("Close"))
        {
            IsOpen = false;
        }
    }

    private Config.Configuration.IpfsModeType _ipfsMode = Config.Configuration.IpfsModeType.Easy;
    private string _ipfsApiUrl = "http://localhost:5001/";
    private Task? _saveTask;
    private void DrawIpfsSetup()
    {
        ImGui.Text("IPFS Setup:");
        if (GuiUtils.GenericEnumCombo<Config.Configuration.IpfsModeType>("IPFS Mode", 100, _ipfsMode, out var newMode,
                Enum.GetValues<Config.Configuration.IpfsModeType>()))
        {
            _ipfsMode = newMode;
        }
        ImGui.Spacing();
        if (_ipfsMode == Config.Configuration.IpfsModeType.Expert)
        {
            ImGui.Text("IPFS API URL:");
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("##IpfsApiUrl", ref _ipfsApiUrl);
            ImGui.Spacing();
        }

        if (_saveTask != null && !_saveTask.IsCompleted)
        {
            ImGui.BeginDisabled();
            ImGui.Button("Saving...");
            ImGui.EndDisabled();
        }
        else
        {
            if (ImGui.Button("Save"))
            {
                _saveTask = Task.Run(async () =>
                {
                    switch (_ipfsMode)
                    {
                        case Config.Configuration.IpfsModeType.Easy:
                            _configuration.IpfsApiUrl = null;
                            if (!await _ipfsService.DownloadAndExtractBinaries())
                            {
                                throw new Exception("Failed to download IPFS binaries!");
                            }
                            _configuration.IpfsMode = _ipfsMode;
                            _configuration.Save();
                            break;
                        case Config.Configuration.IpfsModeType.Expert:
                            _configuration.IpfsApiUrl = _ipfsApiUrl;
                            _configuration.IpfsMode = _ipfsMode;
                            _configuration.Save();
                            break;
                    }
                    _ipfsService.InitEngine();
                });
            }
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("Pocket Sized Universe uses IPFS (InterPlanetary File System) to sync your mods and other glam data to people you pair with.");
        ImGui.Spacing();
        ImGui.Text("You can choose between two modes of operation:");
        ImGui.Text(" - Easy: The PSU plugin handles the IPFS system in the background for you");
        ImGui.Text(" - Expert: You manage your own IPFS daemon and run the PSU plugin against it.");
        ImGui.Spacing();
        ImGui.Text("Easy mode is recommended for most users.");
        if (GuiUtils.IsWine())
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, "When running under Wine, Expert mode is STRONGLY recommended.");
            ImGui.Text("The embedded IPFS daemon may experience connectivity issues when running under Wine.");
        }
    }

    private string _cacheDirectory = "";
    private readonly FileDialogManager _fileDialogManager = new();
    private void DrawCacheSetup()
    {
        ImGui.Text("Cache Directory:");
        ImGui.InputText("##CacheDirectory", ref _cacheDirectory);
        if (ImGui.Button("Browse"))
        {
            _fileDialogManager.OpenFolderDialog("Select Cache Directory", (bool selected, string result) => _cacheDirectory = result);
        }
        ImGui.SameLine();
        if (ImGui.Button("Save"))
        {
            if (!Directory.Exists(_cacheDirectory))
                Directory.CreateDirectory(_cacheDirectory);
            _configuration.CacheDirectory = _cacheDirectory;
            _configuration.Save();
        }
        _fileDialogManager.Draw();
    }
}