using ECommons.DalamudServices;
using Glamourer.Api.IpcSubscribers;

namespace PocketSizedUniverse.Services;

public class GlamourerService
{
    public GlamourerService()
    {
        GetStateBase64 = new GetStateBase64(Svc.PluginInterface);
        ApplyState = new ApplyState(Svc.PluginInterface);
    }
    public GetStateBase64 GetStateBase64 { get; }
    public ApplyState ApplyState { get; }
}