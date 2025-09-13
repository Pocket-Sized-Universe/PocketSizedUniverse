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
        GetPlayerResourceTrees = new GetPlayerResourceTrees(Svc.PluginInterface);
        GetPlayerResourcePaths = new GetPlayerResourcePaths(Svc.PluginInterface);
        GetPlayerMetaManipulations = new GetPlayerMetaManipulations(Svc.PluginInterface);
        AssignTemporaryCollection = new AssignTemporaryCollection(Svc.PluginInterface);
        RedrawObject = new RedrawObject(Svc.PluginInterface);
    }
    public GetModDirectory GetModDirectory { get; }
    public CreateTemporaryCollection CreateTemporaryCollection { get; }
    public AssignTemporaryCollection AssignTemporaryCollection { get; }
    public AddTemporaryMod AddTemporaryMod { get; }
    public GetPlayerResourceTrees GetPlayerResourceTrees { get; }
    public GetPlayerResourcePaths GetPlayerResourcePaths { get; }
    public GetPlayerMetaManipulations GetPlayerMetaManipulations { get; }
    public RedrawObject RedrawObject { get; }
}