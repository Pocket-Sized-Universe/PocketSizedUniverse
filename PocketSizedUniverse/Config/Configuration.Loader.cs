using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using PocketSizedUniverse.Data;

namespace PocketSizedUniverse.Config;

public partial class Configuration
{
    [JsonIgnore] private int LastSavedConfigHash { get; set; }

    [JsonIgnore]
    private static JsonSerializerSettings? SerializerOptions { get; } = new JsonSerializerSettings()
    {
        Converters = { new CidConverter() },
    };

    [JsonIgnore]
    private static IDalamudPluginInterface? _pluginInterface;

    [JsonIgnore]
    private static IPluginLog? _pluginLog;

    public static Config.Configuration Load(IServiceProvider serviceProvider)
    {
        _pluginInterface = serviceProvider.GetRequiredService<IDalamudPluginInterface>();
        _pluginLog = serviceProvider.GetRequiredService<IPluginLog>();

        var fileInfo = _pluginInterface.ConfigFile;
        if (!fileInfo.Exists || fileInfo.Length < 2)
            return new();

        var json = File.ReadAllText(fileInfo.FullName);
        var node = JsonConvert.DeserializeObject<Config.Configuration>(json, SerializerOptions);
        if (node == null)
            return new();

        var serialized = JsonConvert.SerializeObject(node, Formatting.Indented, SerializerOptions);
        node.LastSavedConfigHash = serialized.GetHashCode();
        
        return node;
    }

    public void Save()
    {
        try
        {
            var serialized = JsonConvert.SerializeObject(this, Formatting.Indented, SerializerOptions);
            var hash = serialized.GetHashCode();

            if (LastSavedConfigHash != hash)
            {
                FilesystemUtil.WriteAllTextSafe(_pluginInterface!.ConfigFile.FullName, serialized);
                LastSavedConfigHash = hash;
                _pluginLog?.Information("Configuration saved.");
            }
        }
        catch (Exception e)
        {
            _pluginLog?.Error(e, "Error saving config");
        }
    }
}