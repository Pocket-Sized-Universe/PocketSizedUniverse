using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using HaselCommon.Gui.ImGuiTable;
using HaselCommon.Services;
using Ipfs.CoreApi;
using PocketSizedUniverse.Data;
using PocketSizedUniverse.Controllers;
using PocketSizedUniverse.Services;

namespace PocketSizedUniverse.GUI;

public class ConfigWindow : Window
{
    private readonly Config.Configuration _configuration;
    private readonly ModController _modController;
    private readonly IpfsService _ipfsService;
    public ConfigWindow(Config.Configuration configuration, IpfsService ipfsService, ModController modController) : base("Pocket Sized Universe Config")
    {
        _configuration = configuration;
        _ipfsService = ipfsService;
        _modController = modController;
    }

    private string? _apiUrl;
    private Config.Configuration.IpfsModeType? _mode;
    public override void Draw()
    {
        if (ImGui.BeginTabBar("##Tabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                ImGui.Text("General Settings");
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("IPFS"))
            {
                ImGui.Text("IPFS Settings");
                _mode ??= _configuration.IpfsMode ?? throw new InvalidOperationException("IPFS mode not set yet");
                if (GuiUtils.GenericEnumCombo<Config.Configuration.IpfsModeType>("IPFS Mode", 100, _mode.Value, out var newMode,
                        Enum.GetValues<Config.Configuration.IpfsModeType>()))
                {
                    _mode = newMode;
                }
                if (_mode == Config.Configuration.IpfsModeType.Expert)
                {
                    _apiUrl ??= _configuration.IpfsApiUrl ?? "http://localhost:5001/";
                    ImGui.InputText("IPFS API URL", ref _apiUrl, 200);
                }

                if (ImGui.Button("Save"))
                {
                    if (_configuration.IpfsMode == Config.Configuration.IpfsModeType.Easy &&
                        _mode == Config.Configuration.IpfsModeType.Expert)
                        _ipfsService.StopDaemon();
                    _configuration.IpfsMode = _mode;
                    _configuration.IpfsApiUrl = _apiUrl;
                    _configuration.Save();
                    _ipfsService.DaemonIsReady = false;
                    _ipfsService.InitEngine();
                }
                ImGui.Spacing();
                ImGui.Separator();
                var maxParallel = _configuration.MaxParallelIpfsRequests;
                if (ImGui.SliderInt("Max Parallel IPFS Requests", ref maxParallel, 1, 10))
                {
                    _configuration.MaxParallelIpfsRequests = maxParallel;
                    _configuration.Dirty = true;
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Files"))
            {
                ImGui.Text("Files Settings");
                ImGui.Text($"Cached files: {_modController.CidToFilePathCache.Count}");
                ImGui.Text($"Resolving files: {_modController.FileResolveTasks.Count(kvp => !kvp.Value.IsCompleted)}");
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }
}