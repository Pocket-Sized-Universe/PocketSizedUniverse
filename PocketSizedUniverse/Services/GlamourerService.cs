using ECommons.DalamudServices;
using Glamourer.Api.IpcSubscribers;

namespace PocketSizedUniverse.Services;

public class GlamourerService
{
    public GlamourerService()
    {
        GetState = new GetState(Svc.PluginInterface);
        ApplyState = new ApplyState(Svc.PluginInterface);
    }
    public GetState GetState { get; }
    public ApplyState ApplyState { get; }
}