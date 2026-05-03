using System.Collections.Concurrent;
using Ipfs;
using Microsoft.Extensions.Logging;
using PocketSizedUniverse.Data;
using PocketSizedUniverse.Util;

namespace PocketSizedUniverse.Services;

public class PlayerDataService : IDisposable
{
    private readonly ILogger<PlayerDataService> _logger;
    private readonly IpfsService _ipfsService;
    private readonly Config.Configuration _configuration;
    private readonly CancellationTokenSource _cts = new();

    public PlayerDataService(ILogger<PlayerDataService> logger, IpfsService ipfsService, Config.Configuration configuration)
    {
        _configuration = configuration;
        _logger = logger;
        _ipfsService = ipfsService;
        Task.Run(() => LocalPlayerUpdate(_cts.Token), _cts.Token);
    }

    public bool LocalDataDirty { get; set; } = false;
    public PlayerData? LocalPlayerData { get; set; }
    public Cid? LocalPlayerDataCid { get; set; }
    public ConcurrentDictionary<Guid, RemoteData> PlayerDataByGuid { get; set; } = new();
    public readonly ConcurrentQueue<Guid> GuidsNeedingApplication = [];
    public readonly ConcurrentQueue<Guid> GuidsNeedingRemoval = [];
    
    private string _localPlayerDataBase64 = string.Empty;

    private async Task LocalPlayerUpdate(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (LocalPlayerData != null && _ipfsService.DaemonIsReady)
                {
                    if (LocalDataDirty)
                    {
                        _logger.LogDebug("Local player data changed, updating base64 string");
                        LocalPlayerData.LastModified = DateTime.UtcNow;
                        var base64Text = Base64Util.ToBase64(LocalPlayerData);
                        _localPlayerDataBase64 = base64Text;
                        LocalDataDirty = false;
                    }
                    var myId = _configuration.PairingId;
                    foreach (var pairedGuid in _configuration.IndividualPairs)
                    {
                        var outTopic = TopicUtil.GetPairingTopic(myId, pairedGuid);
                        await _ipfsService.PublishToTopic(outTopic, _localPlayerDataBase64, token);
                    }
                    foreach (var galaxy in _configuration.Galaxies)
                    {
                        var galaxyTopic = TopicUtil.GetGalaxyTopic(galaxy);
                        await _ipfsService.PublishToTopic(galaxyTopic, _localPlayerDataBase64, token);
                    }
                    if (_configuration.GlobalSyncEnabled)
                        await _ipfsService.PublishToTopic(TopicUtil.GetGlobalSyncTopic(), _localPlayerDataBase64, token);
                }
                await Task.Delay(1000, token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Local player data update canceled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in local player data update loop.");
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}