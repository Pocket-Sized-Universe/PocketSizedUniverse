using Dalamud.Plugin;
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
        _logger.LogInformation("GlamourerService initialized");
    }
    public GetStateBase64 GetStateBase64 { get; }
    public ApplyState ApplyState { get; }

    public bool ApplyData(int objectIndex, string glamState)
    {
        var applyResult = ApplyState.Invoke(glamState, objectIndex, LockKey);
        return applyResult == GlamourerApiEc.Success;
    }
}