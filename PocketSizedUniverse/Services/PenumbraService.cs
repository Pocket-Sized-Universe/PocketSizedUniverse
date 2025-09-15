using ECommons.DalamudServices;
using Penumbra;
using Penumbra.Api.IpcSubscribers;

namespace PocketSizedUniverse.Services;

public class PenumbraService
{
    public PenumbraService()
    {
        GetModDirectory = new GetModDirectory(Svc.PluginInterface);
        CreateTemporaryCollection = new CreateTemporaryCollection(Svc.PluginInterface);
        AddTemporaryMod = new AddTemporaryMod(Svc.PluginInterface);
        GetPlayerMetaManipulations = new GetPlayerMetaManipulations(Svc.PluginInterface);
        AssignTemporaryCollection = new AssignTemporaryCollection(Svc.PluginInterface);
        RedrawObject = new RedrawObject(Svc.PluginInterface);
        GetAllModSettings = new GetAllModSettings(Svc.PluginInterface);
        GetCollectionForObject = new GetCollectionForObject(Svc.PluginInterface);
        GetPlayerResourceTrees = new GetPlayerResourceTrees(Svc.PluginInterface);
        SetTemporaryModSettings = new SetTemporaryModSettings(Svc.PluginInterface);
        RemoveTemporaryMod = new RemoveTemporaryMod(Svc.PluginInterface);
        GetGameObjectResourcePaths = new GetGameObjectResourcePaths(Svc.PluginInterface);
    }
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
}