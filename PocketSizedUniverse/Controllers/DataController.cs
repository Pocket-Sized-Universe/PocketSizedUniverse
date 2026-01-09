using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.ImGuiMethods;
using Glamourer.Api.IpcSubscribers;
using Ipfs;
using Ipfs.Http;
using Microsoft.Extensions.Logging;
using Multiformats.Base;
using PocketSizedUniverse.Data;
using PocketSizedUniverse.Services;
using PocketSizedUniverse.Util;

namespace PocketSizedUniverse.Controllers;

public class DataController : IDisposable
{
    private ILogger<DataController> _logger;
    private readonly IObjectTable _objectTable;
    private readonly GlamourerService _gamourerService;
    private readonly HonorificService _honorificService;
    private readonly MoodlesService _moodlesService;
    private readonly PetNameService _petNameService;
    private readonly SimpleHeelsService _simpleHeelsService;
    private readonly IpfsService _ipfsService;
    private readonly CustomizeService _customizeService;
    private readonly IFramework _framework;
    private readonly Config.Configuration _configuration;
    private readonly ModController _modController;
    private readonly PlayerDataService _playerDataService;
    private readonly IDalamudPluginInterface _pluginInterface;

    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _subscribedTopics = new();

    public DataController(ILogger<DataController> logger, IObjectTable objectTable, GlamourerService gamourerService,
        HonorificService honorificService, MoodlesService moodlesService, PetNameService petNameService,
        SimpleHeelsService simpleHeelsService, CustomizeService customizeService, IpfsService ipfsService,
        IFramework framework, Config.Configuration configuration, ModController modController,
        PlayerDataService playerDataService, IDalamudPluginInterface pluginInterface)
    {
        _logger = logger;
        _configuration = configuration;
        _objectTable = objectTable;
        _gamourerService = gamourerService;
        _honorificService = honorificService;
        _moodlesService = moodlesService;
        _petNameService = petNameService;
        _simpleHeelsService = simpleHeelsService;
        _customizeService = customizeService;
        _ipfsService = ipfsService;
        _modController = modController;
        _playerDataService = playerDataService;
        _framework = framework;
        _pluginInterface = pluginInterface;
        _framework.Update += OnUpdate;
        _modController.PathsReady += OnPathsReady;
        _logger.LogInformation("Data Controller created");
        StateChanged.Subscriber(pluginInterface, OnGlamourerStateChanged).Enable();
        Task.Run(() => DoBackgroundUpdate(_cts.Token), _cts.Token);
    }

    private void OnGlamourerStateChanged(nint address)
    {
        var capturedAddress = address;
        _ = Task.Run(async () =>
        {
            _logger.LogDebug("Glamourer state changed for {Address}", capturedAddress);
            await _framework.RunOnFrameworkThread(() =>
            {
                var player = _objectTable.LocalPlayer;
                if (player?.Address == capturedAddress && _playerDataService.LocalPlayerData != null)
                {
                    Task.Run(() =>
                    {
                        _logger.LogDebug("Updating local player data");
                        var glamState = _gamourerService.GetStateBase64.Invoke(player.ObjectIndex).Item2;
                        _playerDataService.LocalPlayerData.GlamourerState = glamState;
                        _playerDataService.LocalDataDirty = true;
                    });
                }
            });
        });
    }

    private async Task DoBackgroundUpdate(CancellationToken token)
    {
        var parallelOptions = new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = 3 };
        while (!token.IsCancellationRequested)
        {
            try
            {
                var subbedTopics = await _ipfsService.GetSubscribedTopics();
                
                var myId = _configuration.PairingId;
                foreach (var pairedGuid in _configuration.IndividualPairs)
                {
                    var topic = TopicUtil.GetPairingTopic(pairedGuid, myId);
                    if (subbedTopics.Contains(topic)) continue;
                    var subCts = new CancellationTokenSource();
                    await _ipfsService.SubscribeToTopic(topic, HandleSubMessage, subCts.Token);
                    _subscribedTopics[topic] = subCts;
                    _logger.LogDebug("Subscribed to topic {Topic} for pairing with {Guid}", topic, pairedGuid);
                }

                foreach (var galaxy in _configuration.Galaxies)
                {
                    var topic = TopicUtil.GetGalaxyTopic(galaxy);
                    if (subbedTopics.Contains(topic)) continue;
                    var subCts = new CancellationTokenSource();
                    await _ipfsService.SubscribeToTopic(topic, HandleSubMessage, subCts.Token);
                    _subscribedTopics[topic] = subCts;
                    //_logger.LogDebug("Subscribed to topic {Topic} for galaxy {Galaxy}", topic, galaxy);
                }

                foreach (var subbedTopic in subbedTopics)
                {
                    if (!TopicUtil.IsPairingTopic(subbedTopic) || !TopicUtil.IsGalaxyTopic(subbedTopic)) continue;
                    var pairedId = TopicUtil.TopicToPairedGuid(subbedTopic);
                    if (pairedId == null) continue;
                    if (_configuration.IndividualPairs.Contains(pairedId.Value) ||
                        _configuration.Galaxies.Contains(pairedId.Value) ||
                        !_subscribedTopics.TryGetValue(subbedTopic, out var subCts)) continue;
                    await subCts.CancelAsync();
                    _subscribedTopics.TryRemove(subbedTopic, out _);
                    //_logger.LogDebug("Unsubscribed from topic {Topic}", subbedTopic);
                }

                switch (_configuration.GlobalSyncEnabled)
                {
                    case true when !subbedTopics.Contains(TopicUtil.GetGlobalSyncTopic()):
                    {
                        var subCts = new CancellationTokenSource();
                        await _ipfsService.SubscribeToTopic(TopicUtil.GetGlobalSyncTopic(), HandleSubMessage, subCts.Token);
                        _subscribedTopics[TopicUtil.GetGlobalSyncTopic()] = subCts;
                        break;
                    }
                    case false when subbedTopics.Contains(TopicUtil.GetGlobalSyncTopic()):
                    {
                        if (_subscribedTopics.TryGetValue(TopicUtil.GetGlobalSyncTopic(), out var subCts))
                        {
                            await subCts.CancelAsync();
                            _subscribedTopics.TryRemove(TopicUtil.GetGlobalSyncTopic(), out _);
                        }
                        else
                        {
                            _logger.LogWarning("Global sync topic was subscribed to, but not found in subscribed topics list");
                        }
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Data update canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background data update loop.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), token);
        }
    }

    private void HandleSubMessage(IPublishedMessage message)
    {
        if (message is not PublishedMessage pubMessage)
            return;
        var json = pubMessage.DataString;
        var dataObj = Base64Util.FromBase64<PlayerData>(json);
        if (dataObj is null)
            return;

        var pairedGuid = dataObj.PairId;
        if (pairedGuid == _configuration.PairingId)
            return;

        if (!_playerDataService.PlayerDataByGuid.TryGetValue(pairedGuid, out var data))
        {
            var remoteData = new RemoteData()
            {
                PlayerData = dataObj,
                PairId = pairedGuid,
                Dirty = true,
            };
            _playerDataService.PlayerDataByGuid.TryAdd(pairedGuid, remoteData);
        }
        else if (data.PlayerData?.LastModified < dataObj.LastModified)
        {
            data.PlayerData = dataObj;
            data.Dirty = true;
        }
    }

    private void OnPathsReady(object? sender, ModController.PathsReadyEventArgs e)
    {
        if (GuidsNeedingApplication.Contains(e.PairId)) return;
        GuidsNeedingApplication.Enqueue(e.PairId);
        _logger.LogDebug("Queued guid {Guid} for application", e.PairId);
    }

    public readonly ConcurrentQueue<Guid> GuidsNeedingApplication = [];


    private DateTime _lastUpdate = DateTime.MinValue;

    private void OnUpdate(IFramework framework)
    {
        if (DateTime.Now - _lastUpdate < TimeSpan.FromSeconds(5)) return;
        _lastUpdate = DateTime.Now;
        var player = _objectTable.LocalPlayer;
        if (player == null || !GenericHelpers.IsScreenReady())
        {
            _playerDataService.LocalPlayerData = null;
            return;
        }

        _playerDataService.LocalPlayerData ??= new PlayerData()
        {
            EntityId = player.EntityId,
            PairId = _configuration.PairingId,
            GlamourerState = _gamourerService.GetStateBase64.Invoke(player.ObjectIndex).Item2
        };

        var honorificTitle = _honorificService.GetLocalCharacterTitle();
        if (_playerDataService.LocalPlayerData.HonorificTitle != honorificTitle)
        {
            _playerDataService.LocalPlayerData.HonorificTitle = honorificTitle;
            _playerDataService.LocalDataDirty = true;
        }

        var moodlesState = _moodlesService.GetStatusManager(player.Address);
        if (_playerDataService.LocalPlayerData.MoodlesState != moodlesState)
        {
            _playerDataService.LocalPlayerData.MoodlesState = moodlesState;
            _playerDataService.LocalDataDirty = true;
        }

        var petNameState = _petNameService.GetPlayerData();
        if (_playerDataService.LocalPlayerData.PetNameState != petNameState)
        {
            _playerDataService.LocalPlayerData.PetNameState = petNameState;
            _playerDataService.LocalDataDirty = true;
        }

        var heelsState = _simpleHeelsService.GetLocalPlayer();
        if (_playerDataService.LocalPlayerData.HeelsState != heelsState)
        {
            _playerDataService.LocalPlayerData.HeelsState = heelsState;
            _playerDataService.LocalDataDirty = true;
        }

        var customizeProfile = _customizeService.GetActiveProfileOnCharacter(player.ObjectIndex).Item2;
        if (customizeProfile != null)
        {
            var profileData = _customizeService.GetCustomizeProfileByUniqueId(customizeProfile.Value).Item2;
            if (_playerDataService.LocalPlayerData.CustomizeState != profileData)
            {
                _playerDataService.LocalPlayerData.CustomizeState = profileData;
                _playerDataService.LocalDataDirty = true;
            }
        }

        foreach (var playerData in _playerDataService.PlayerDataByGuid.Values)
        {
            var playerObj =
                _objectTable.PlayerObjects.FirstOrDefault(p => p.EntityId == playerData.PlayerData?.EntityId);
            if (playerObj == null || !playerData.Dirty)
                continue;
            if (!_playerDataProcessingTasks.TryGetValue(playerData, out var task) || task.IsCompleted)
            {
                _playerDataProcessingTasks[playerData] = Task.Run(async () => await _modController.PreparePaths(playerData), _cts.Token);
            }
        }

        if (!GuidsNeedingApplication.TryDequeue(out var cid))
            return;
        if (!_playerDataService.PlayerDataByGuid.TryGetValue(cid, out var data) || data.PlayerData == null)
            return;

        var remoteData = data.PlayerData;
        var remotePlayer = _objectTable.PlayerObjects.Cast<IPlayerCharacter>()
            .FirstOrDefault(p => p.EntityId == remoteData.EntityId && p.EntityId != player.EntityId);
        if (remotePlayer == null) return;

        if (remoteData.GlamourerState != null)
            _gamourerService.ApplyData(remotePlayer.ObjectIndex, remoteData.GlamourerState);

        if (remoteData.CustomizeState != null)
            _customizeService.ApplyData(remotePlayer.ObjectIndex, remoteData.CustomizeState);

        if (remoteData.MoodlesState != null)
            _moodlesService.ApplyData(remotePlayer.Address, remoteData.MoodlesState);

        if (remoteData.HonorificTitle != null)
            _honorificService.ApplyData(remotePlayer.ObjectIndex, remoteData.HonorificTitle);

        if (remoteData.HeelsState != null)
            _simpleHeelsService.ApplyData(remotePlayer.ObjectIndex, remoteData.HeelsState);

        if (remoteData.PetNameState != null)
            _petNameService.ApplyData(remoteData.PetNameState);

        var collId = _modController.ApplyData(remotePlayer.ObjectIndex, remoteData.MetaManipulations ?? string.Empty,
            data.PreparedPaths ?? new Dictionary<string, string>(), data.CollectionId, cid);
        data.CollectionId = collId;
        data.Dirty = false;
        _logger.LogInformation("Applied data for {Entity}", remoteData.EntityId);
    }
    
    private readonly ConcurrentDictionary<RemoteData, Task> _playerDataProcessingTasks = new();

    public void Dispose()
    {
        StateChanged.Subscriber(_pluginInterface, OnGlamourerStateChanged).Disable();
        _framework.Update -= OnUpdate;
        foreach (var data in _playerDataService.PlayerDataByGuid)
        {
            if (data.Value.CollectionId != null)
                _modController.CleanupData(data.Value.CollectionId.Value, data.Key);
        }

        foreach (var cts in _subscribedTopics)
        {
            cts.Value.Cancel();
        }

        _cts.Cancel();
        _cts.Dispose();
        _updateLock.Dispose();
    }
}