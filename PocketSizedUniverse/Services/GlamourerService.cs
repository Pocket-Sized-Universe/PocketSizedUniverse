using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;
using Microsoft.Extensions.Logging;

namespace PocketSizedUniverse.Services;

public class GlamourerService
{
    public const uint LockKey = 8675309;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ILogger<GlamourerService> _logger;
    public GlamourerService(IDalamudPluginInterface pluginInterface, ILogger<GlamourerService> logger)
    {
        _pluginInterface = pluginInterface;
        _logger = logger;
        GetStateBase64 = new GetStateBase64(_pluginInterface);
        ApplyState = new ApplyState(_pluginInterface);
        RevertState = new RevertState(_pluginInterface);
        _logger.LogInformation("GlamourerService initialized");
    }
    private GetStateBase64 GetStateBase64 { get; }
    private ApplyState ApplyState { get; }
    private RevertState RevertState { get; }

    public string? GetData(int objectIndex)
    {
        var state = GetStateBase64.Invoke(objectIndex, LockKey);
        return state.Item1 == GlamourerApiEc.Success ? state.Item2 : null;
    }
    
    public bool ApplyData(int objectIndex, string glamState)
    {
        try
        {
            var applyResult = ApplyState.Invoke(glamState, objectIndex, LockKey);
            return applyResult == GlamourerApiEc.Success;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
    }
    
    public bool RevertData(int objectIndex)
    {
        try
        {
            var revertResult = RevertState.Invoke(objectIndex, LockKey);
            return revertResult == GlamourerApiEc.Success;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
    }
}