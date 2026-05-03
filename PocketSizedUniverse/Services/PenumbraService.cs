using Dalamud.Plugin;
using PocketSizedUniverse.Data;
using Microsoft.Extensions.Logging;
using Penumbra.Api.IpcSubscribers;

namespace PocketSizedUniverse.Services;

public class PenumbraService
{
    private readonly ILogger<PenumbraService> _logger;
    private readonly IDalamudPluginInterface _pluginInterface;
    public PenumbraService(IDalamudPluginInterface pluginInterface, ILogger<PenumbraService> logger)
    {
        _pluginInterface = pluginInterface;
        _logger = logger;
        GetModDirectory = new GetModDirectory(_pluginInterface);
        CreateTemporaryCollection = new CreateTemporaryCollection(_pluginInterface);
        AddTemporaryMod = new AddTemporaryMod(_pluginInterface);
        GetPlayerMetaManipulations = new GetPlayerMetaManipulations(_pluginInterface);
        AssignTemporaryCollection = new AssignTemporaryCollection(_pluginInterface);
        RedrawObject = new RedrawObject(_pluginInterface);
        GetAllModSettings = new GetAllModSettings(_pluginInterface);
        GetCollectionForObject = new GetCollectionForObject(_pluginInterface);
        GetPlayerResourceTrees = new GetPlayerResourceTrees(_pluginInterface);
        SetTemporaryModSettings = new SetTemporaryModSettings(_pluginInterface);
        RemoveTemporaryMod = new RemoveTemporaryMod(_pluginInterface);
        GetGameObjectResourcePaths = new GetGameObjectResourcePaths(_pluginInterface);
        DeleteTemporaryCollection = new DeleteTemporaryCollection(_pluginInterface);
        GetModList = new GetModList(_pluginInterface);
        GetChangedItems = new GetChangedItems(_pluginInterface);
        GetPlayerResourcePaths = new GetPlayerResourcePaths(_pluginInterface);
        GetCurrentModSettings = new GetCurrentModSettings(_pluginInterface);
        _logger.LogInformation("Penumbra service initialized");
    }
    public GetChangedItems GetChangedItems { get; }
    public GetModList GetModList { get; }
    public GetModDirectory GetModDirectory { get; }
    public CreateTemporaryCollection CreateTemporaryCollection { get; }
    public AssignTemporaryCollection AssignTemporaryCollection { get; }
    public AddTemporaryMod AddTemporaryMod { get; }
    public GetPlayerMetaManipulations GetPlayerMetaManipulations { get; }
    public RedrawObject RedrawObject { get; }
    public GetCollectionForObject GetCollectionForObject { get;  }
    public GetAllModSettings GetAllModSettings { get; }
    public GetPlayerResourceTrees GetPlayerResourceTrees { get; }
    public SetTemporaryModSettings SetTemporaryModSettings { get; }
    public RemoveTemporaryMod RemoveTemporaryMod { get; }
    public GetGameObjectResourcePaths GetGameObjectResourcePaths { get; }
    public DeleteTemporaryCollection DeleteTemporaryCollection { get; }
    public GetPlayerResourcePaths GetPlayerResourcePaths { get; }
    public GetCurrentModSettings GetCurrentModSettings { get; }
}