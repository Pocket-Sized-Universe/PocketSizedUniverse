using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.EzIpcManager;
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
    private readonly GlamourerService _glamourerService;
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

    public DataController(ILogger<DataController> logger, IObjectTable objectTable, GlamourerService glamourerService,
        HonorificService honorificService, MoodlesService moodlesService, PetNameService petNameService,
        SimpleHeelsService simpleHeelsService, CustomizeService customizeService, IpfsService ipfsService,
        IFramework framework, Config.Configuration configuration, ModController modController,
        PlayerDataService playerDataService, IDalamudPluginInterface pluginInterface)
    {
        _logger = logger;
        _configuration = configuration;
        _objectTable = objectTable;
        _glamourerService = glamourerService;
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
        EzIPC.Init(this);
    }

    [EzIPCEvent("CustomizePlus.Profile.OnUpdate", applyPrefix: false)]
    // ReSharper disable once UnusedMember.Local
    private void OnProfileUpdate(ushort objectIndex, Guid profileId)
    {
        _logger.LogDebug("Profile updated for {ObjectIndex} with ID {ProfileId}", objectIndex, profileId);
        _ = Task.Run(async () =>
        {
            await _framework.RunOnFrameworkThread(() =>
            {
                if (_objectTable.LocalPlayer?.ObjectIndex != objectIndex) return;
                var data = _customizeService.GetData(objectIndex);
                if (_playerDataService.LocalPlayerData == null) return;
                _playerDataService.LocalPlayerData.CustomizeState = data;
                _playerDataService.LocalDataDirty = true;
            });
        });
    }

    [EzIPCEvent("PetRenamer.OnPlayerDataChanged", actionLastGenericType: typeof(object), applyPrefix: false)]
    // ReSharper disable once UnusedMember.Local
    private void OnPetNameDataChanged(string obj)
    {
        _logger.LogDebug("Pet name data changed for {Player}", obj);
        _playerDataService.LocalPlayerData?.PetNameState = _petNameService.GetData();
        _playerDataService.LocalDataDirty = true;
    }

    [EzIPCEvent("Moodles.StatusManagerModified", actionLastGenericType: typeof(object), applyPrefix: false)]
    // ReSharper disable once UnusedMember.Local
    private void OnStatusManagerModified(nint obj)
    {
        _logger.LogDebug("Status manager modified for {Address}", obj);
        _ = Task.Run(async () =>
        {
            await _framework.RunOnFrameworkThread(() =>
            {
                var player = _objectTable.LocalPlayer;
                var realObj = _objectTable.CreateObjectReference(obj);
                if (player?.Address != realObj?.Address) return;
                var statusManager = _moodlesService.GetData(obj);
                _playerDataService.LocalPlayerData?.MoodlesState = statusManager;
                _playerDataService.LocalDataDirty = true;
            });
        });
    }

    [EzIPCEvent("Honorific.LocalCharacterTitleChanged", actionLastGenericType: typeof(object), applyPrefix: false)]
    // ReSharper disable once UnusedMember.Local
    private void OnLocalCharacterTitleChanged(string obj)
    {
        _logger.LogDebug("Local character title changed to {Title}", obj);
        _playerDataService.LocalPlayerData?.HonorificTitle = obj;
        _playerDataService.LocalDataDirty = true;
    }

    private void OnGlamourerStateChanged(nint address)
    {
        var capturedAddress = address;
        _ = Task.Run(async () =>
        {
            //_logger.LogDebug("Glamourer state changed for {Address}", capturedAddress);
            await _framework.RunOnFrameworkThread(() =>
            {
                var player = _objectTable.LocalPlayer;
                if (player?.Address == capturedAddress && _playerDataService.LocalPlayerData != null)
                {
                    Task.Run(() =>
                    {
                        _logger.LogDebug("Updating local player data");
                        var glamState = _glamourerService.GetData(player.ObjectIndex);
                        _playerDataService.LocalPlayerData.GlamourerState = glamState;
                        _ = _modController.UpdatePenumbraData();
                        _playerDataService.LocalDataDirty = true;
                    });
                }
            });
        });
    }

    private async Task DoBackgroundUpdate(CancellationToken token)
    {
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
                        await _ipfsService.SubscribeToTopic(TopicUtil.GetGlobalSyncTopic(), HandleSubMessage,
                            subCts.Token);
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
                            _logger.LogWarning(
                                "Global sync topic was subscribed to, but not found in subscribed topics list");
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
        if (pairedGuid == _configuration.PairingId || _configuration.BlockedIds.Contains(pairedGuid))
            return;

        var currentWorld = dataObj.CurrentWorld;
        if (currentWorld != _currentWorldId) return;

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
        if (_playerDataService.GuidsNeedingApplication.Contains(e.PairId)) return;
        _playerDataService.GuidsNeedingApplication.Enqueue(e.PairId);
        _logger.LogDebug("Queued guid {Guid} for application", e.PairId);
    }

    private DateTime _lastUpdate = DateTime.MinValue;
    private uint? _currentWorldId;

    private void OnUpdate(IFramework framework)
    {
        if (DateTime.Now - _lastUpdate < TimeSpan.FromSeconds(1)) return;
        _lastUpdate = DateTime.Now;
        var player = _objectTable.LocalPlayer;
        if (player == null || !GenericHelpers.IsScreenReady())
        {
            _currentWorldId = null;
            _playerDataService.LocalPlayerData = null;
            return;
        }

        _currentWorldId ??= player.CurrentWorld.RowId;

        if (_playerDataService.LocalPlayerData == null)
        {
            _playerDataService.LocalPlayerData = new PlayerData()
            {
                EntityId = player.EntityId,
                PairId = _configuration.PairingId,
                CurrentWorld = player.CurrentWorld.RowId,
                GlamourerState = _glamourerService.GetData(player.ObjectIndex),
                HonorificTitle = _honorificService.GetData(player.ObjectIndex),
                MoodlesState = _moodlesService.GetData(player.Address),
                PetNameState = _petNameService.GetData(),
            };

            _ = _modController.UpdatePenumbraData();
            _playerDataService.LocalDataDirty = true;
        }

        var heelsState = _simpleHeelsService.GetData();
        if (_playerDataService.LocalPlayerData.HeelsState != heelsState)
        {
            _playerDataService.LocalPlayerData.HeelsState = heelsState;
            _playerDataService.LocalDataDirty = true;
        }

        foreach (var playerData in _playerDataService.PlayerDataByGuid.Values)
        {
            var playerObj =
                _objectTable.PlayerObjects.FirstOrDefault(p => p.EntityId == playerData.PlayerData?.EntityId);
            if (playerObj == null && playerData.CollectionId != null)
            {
                _playerDataService.GuidsNeedingRemoval.Enqueue(playerData.PairId);
                continue;
            }

            if (playerObj == null || !playerData.Dirty)
                continue;
            if (!_playerDataProcessingTasks.TryGetValue(playerData.PairId, out var task) || task.IsCompleted)
            {
                _playerDataProcessingTasks[playerData.PairId] = Task.Run(
                    async () => await _modController.PreparePaths(playerData).ContinueWith((obj) =>
                    {
                        _playerDataProcessingTasks.TryRemove(playerData.PairId, out _);
                    }), _cts.Token);
            }
        }

        if (_playerDataService.GuidsNeedingRemoval.TryDequeue(out var guid))
        {
            if (_playerDataService.PlayerDataByGuid.TryGetValue(guid, out var playerData))
            {
                var obj =
                    _objectTable.PlayerObjects.FirstOrDefault(o => o.EntityId == playerData.PlayerData?.EntityId);
                if (obj != null)
                {
                    _customizeService.RevertData(obj.ObjectIndex);
                    _honorificService.RevertData(obj.ObjectIndex);
                    _moodlesService.RevertData(obj.Address);
                    _simpleHeelsService.RevertData(obj.ObjectIndex);
                    _glamourerService.RevertData(obj.ObjectIndex);
                }

                _modController.CleanupData(_playerDataService.PlayerDataByGuid[guid].CollectionId!.Value, guid,
                    obj?.ObjectIndex);
                _playerDataService.PlayerDataByGuid.TryRemove(guid, out _);
            }
        }

        if (!_playerDataService.GuidsNeedingApplication.TryDequeue(out var cid))
            return;
        if (!_playerDataService.PlayerDataByGuid.TryGetValue(cid, out var data) || data.PlayerData == null)
            return;

        var remoteData = data.PlayerData;
        var remotePlayer = _objectTable.PlayerObjects.Cast<IPlayerCharacter>()
            .FirstOrDefault(p => p.EntityId == remoteData.EntityId && p.EntityId != player.EntityId);
        if (remotePlayer == null) return;

        if (remoteData.GlamourerState != null)
            _glamourerService.ApplyData(remotePlayer.ObjectIndex, remoteData.GlamourerState);

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

    private readonly ConcurrentDictionary<Guid, Task> _playerDataProcessingTasks = new();

    public void Dispose()
    {
        StateChanged.Subscriber(_pluginInterface, OnGlamourerStateChanged).Disable();
        _framework.Update -= OnUpdate;
        foreach (var data in _playerDataService.PlayerDataByGuid)
        {
            if (data.Value.CollectionId != null)
                _modController.CleanupData(data.Value.CollectionId.Value, data.Key, null);
        }

        foreach (var cts in _subscribedTopics)
        {
            cts.Value.Cancel();
        }

        _cts.Cancel();
        _cts.Dispose();
        _updateLock.Dispose();
        GC.SuppressFinalize(this);
    }
}