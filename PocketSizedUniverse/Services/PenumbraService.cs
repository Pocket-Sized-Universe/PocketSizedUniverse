using ECommons.DalamudServices;
using Penumbra;
using Penumbra.Api.IpcSubscribers;

namespace PocketSizedUniverse.Services;

public class PenumbraService
{
    public PenumbraService()
    {
        GetModDirectory = new GetModDirectory(Svc.PluginInterface);
    }
    public GetModDirectory GetModDirectory { get; }
}