using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons.ImGuiMethods;
using PocketSizedUniverse.Services;
using PocketSizedUniverse.Util;

namespace PocketSizedUniverse.GUI;

public class SetupWindow : Window
{
    private readonly Config.Configuration _configuration;
    private readonly IpfsService _ipfsService;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly string _eulaText;
    public SetupWindow(Config.Configuration configuration, IpfsService ipfsService, IDalamudPluginInterface pluginInterface) : base("Pocket Sized Universe Setup", ImGuiWindowFlags.Modal)
    {
        _configuration = configuration;
        _ipfsService = ipfsService;
        _pluginInterface = pluginInterface;
        _eulaText = File.ReadAllText(Path.Combine(_pluginInterface.AssemblyLocation.DirectoryName!, "EULA.txt"));
    }

    public override void Draw()
    {
        if (!_configuration.EulaAccepted)
        {
            DrawEula();
            DrawEulaAccept();
        }
        else if (string.IsNullOrEmpty(_configuration.CacheDirectory))
            DrawCacheSetup();
        else if (_configuration.IpfsMode == null && !(_saveTask?.IsCompletedSuccessfully ?? false))
            DrawIpfsSetup();
        else
            DrawWelcome();
    }

    private bool _accepted = false;
    private void DrawEulaAccept()
    {
        ImGui.Checkbox("I have read and accept the EULA", ref _accepted);
        ImGui.Spacing();
        if (!_accepted)
            ImGui.BeginDisabled();
        if (ImGui.Button("Accept"))
        {
            _configuration.EulaAccepted = true;
            _configuration.Save();
        }
        if (!_accepted)
            ImGui.EndDisabled();
    }

    private void DrawEula()
    {
        var footerHeight = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeightWithSpacing() * 2;
        using var child = ImRaii.Child("EulaText", new Vector2(0, -footerHeight), true);
        if (!child) return;
        
        ImGui.TextWrapped(_eulaText);
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