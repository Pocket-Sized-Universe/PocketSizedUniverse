using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using Ipfs;
using Microsoft.Extensions.Logging;
using PocketSizedUniverse.Data;
using PocketSizedUniverse.Services;

namespace PocketSizedUniverse.GUI;

public class CreateEditGalaxyWindow : Window
{
    private readonly ILogger<CreateEditGalaxyWindow> _logger;
    private readonly Config.Configuration _configuration;
    private readonly IpfsService _ipfsService;
    public CreateEditGalaxyWindow(Config.Configuration configuration, IpfsService ipfsService, ILogger<CreateEditGalaxyWindow> logger) : base("Edit Galaxy")
    {
        _logger = logger;
        _configuration = configuration;
        _ipfsService = ipfsService;
    }

    private Cid? _keyCid;
    private GalaxyData? _galaxyData;
    private Task? _generateKeyTask;
    public void OpenForGalaxy(Cid? keyCid, GalaxyData data)
    {
        _keyCid = keyCid;
        _galaxyData = data;
        _galaxyName = _galaxyData.GalaxyName;
        _galaxyDescription = _galaxyData.Description;
        IsOpen = true;
    }

    private string _galaxyName = "My New Galaxy!";
    private string _galaxyDescription = "A new galaxy!";
    public override void Draw()
    {
        if (_galaxyData == null)
        {
            return;
        }
        
        ImGui.Text("Galaxy Name:");
        ImGui.InputText("##GalaxyName", ref _galaxyName, 256);
        ImGui.Spacing();
        ImGui.Text("Galaxy Description:");
        ImGui.InputTextMultiline("##GalaxyDescription", ref _galaxyDescription, 1024, new Vector2(0, 100));
        ImGui.Spacing();
        if (_saveGalaxyTask != null && !_saveGalaxyTask.IsCompleted)
        {
            ImGui.BeginDisabled();
            ImGui.Button("Saving...");
            ImGui.EndDisabled();
        }
        else
        {
            if (ImGui.Button("Save Galaxy"))
            {
                SaveGalaxy();
            }
        }
        ImGui.Spacing();
        if (ImGui.Button("Add Star Code"))
        {
            PairNewCode();
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("Star Codes:");
        Cid? cidToRemove = null;
        foreach (var cid in _galaxyData.CidsToSubscribe)
        {
            if (ImGui.Selectable($"{cid}"))
            {
                ImGui.SetClipboardText(cid.ToString());
                Notify.Success("Star code copied to clipboard!");
            }

            if (ImGui.BeginPopupContextItem($"##star_context_{cid}"))
            {
                if (ImGui.MenuItem("Remove"))
                {
                    cidToRemove = cid;
                }

                ImGui.EndPopup();
            }
        }
        if (cidToRemove != null)
        {
            _galaxyData.CidsToSubscribe.Remove(cidToRemove);
        }
    }
    
    private Task? _saveGalaxyTask;
    private Task? _starAddTask;
    private void SaveGalaxy()
    {
        _saveGalaxyTask = Task.Run(async () =>
        {
            try
            {
                if (_galaxyData == null) return;
                if (_keyCid == null)
                {
                    var key = await _ipfsService.GenerateKey($"Galaxy {_galaxyName}");
                    _keyCid = key.Id;
                }
                _galaxyData.GalaxyName = _galaxyName;
                _galaxyData.Description = _galaxyDescription;
                    
                var galaxyDataBase = Base64Util.ToBase64(_galaxyData);
                var dataCid = await _ipfsService.AddAndPinText(galaxyDataBase);
                await _ipfsService.PublishGalaxyData(dataCid, _ipfsService.AvailableKeys.First(k => k.Id == _keyCid));
                    
                _configuration.ControlledGalaxies[_keyCid] = _galaxyData;
                _configuration.Save();
                Notify.Success("Galaxy saved!");
            }
            catch (Exception ex)
            {
                Notify.Error("Error saving galaxy!");
                _logger.LogError(ex, "Error saving galaxy!");
            }
        });
    }
    private void PairNewCode()
    {
        var text = ImGui.GetClipboardText();
        _starAddTask = Task.Run(async () =>
        {
            try
            {
                if (string.IsNullOrEmpty(text) || text.StartsWith("Qm"))
                    throw new FormatException();
                
                if (_galaxyData == null) return;

                var cidToPair = Cid.Decode(text);

                if (_galaxyData.CidsToSubscribe.Contains(cidToPair))
                {
                    Notify.Error("Already paired!");
                    return;
                }
                
            
                var dataCid = await _ipfsService.ResolveName(cidToPair.ToString());
                if (dataCid == null)
                {
                    Notify.Error("Star not found!");
                    return;
                }
                var playerDataPath = await _ipfsService.ResolveCidToFilePath(dataCid);
                if (playerDataPath == null)
                {
                    Notify.Error("Star not found!");
                    return;
                }
                var playerDataBase = await File.ReadAllTextAsync(playerDataPath);
                var playerData = Base64Util.FromBase64<PlayerData>(playerDataBase);
                if (playerData == null)
                {
                    Notify.Error("Star data was not valid.");
                    return;
                }

                _galaxyData.CidsToSubscribe.Add(cidToPair);
                Notify.Success("Pairing code added!");
            }
            catch (FormatException)
            {
                Notify.Error("Invalid Pair Code!");
            }
        });
    }
}