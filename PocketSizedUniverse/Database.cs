using System.Collections.Concurrent;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json;
using Penumbra.Api.Enums;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models;

namespace PocketSizedUniverse;

public class Database : IDisposable
{
    private static readonly string _filePath =
        Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "database.json");

    public ConcurrentDictionary<string, HashSet<string>> TransientFilesDataSimple { get; set; } = new();

    public ConcurrentDictionary<string, ScanResult> ScanResults { get; set; } = new();

    [JsonIgnore] public bool SaveNeeded = false;

    public static Database Load()
    {
        if (!File.Exists(_filePath))
            return new Database();
        var json = File.ReadAllText(_filePath);
        return JsonConvert.DeserializeObject<Database>(json) ?? new Database();
    }

    private void Save()
    {
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(_filePath, json);
    }

    public Database()
    {
        Svc.Framework.Update += CleanDatabase;
    }

    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(10);

    [JsonIgnore] public DateTime LastUpdated = DateTime.UtcNow + TimeSpan.FromSeconds(5);

    private void CleanDatabase(IFramework framework)
    {
        if (SaveNeeded)
        {
            Save();
            Svc.Log.Debug("Saved database.");
            SaveNeeded = false;
        }

        if (DateTime.UtcNow - LastUpdated < _updateInterval)
            return;
        LastUpdated = DateTime.UtcNow;
        if (Player.Object == null)
            return;
        var activeCollection = PsuPlugin.PenumbraService.GetCollectionForObject.Invoke(Player.Object.ObjectIndex);
        if (!activeCollection.ObjectValid)
            return;
        Dictionary<string, string> enabledMods = new();
        var allMods = PsuPlugin.PenumbraService.GetModList.Invoke();
        foreach (var (path, name) in allMods)
        {
            var settings =
                PsuPlugin.PenumbraService.GetCurrentModSettings.Invoke(activeCollection.EffectiveCollection.Id, path,
                    name);
            if (settings is { Item1: PenumbraApiEc.Success, Item2.Item1: true })
            {
                enabledMods.Add(path, name);
            }
        }

        foreach (var (realPath, applicablePaths) in TransientFilesDataSimple)
        {
            if (enabledMods.Keys.Any(modPath => realPath.Contains(modPath))) continue;
            TransientFilesDataSimple.TryRemove(realPath, out _);
            Svc.Log.Debug("Removed expired transient file: " + realPath);
            SaveNeeded = true;
        }
    }

    public void Dispose()
    {
        Svc.Framework.Update -= CleanDatabase;
    }
}