using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.EzIpcManager;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Models.Data;
using PocketSizedUniverse.Models.Mods;

namespace PocketSizedUniverse.Services;

public class PlayerDataService : IUpdatable, IDisposable
{
    public PlayerDataService()
    {
        Svc.Framework.Update += LocalUpdate;
        Svc.Framework.Update += RemoteUpdate;
        Svc.ClientState.Logout += OnLogout;
        //StateChanged.Subscriber(Svc.PluginInterface, OnGlamourerStateChanged).Enable();
        //GameObjectRedrawn.Subscriber(Svc.PluginInterface, OnRedraw).Enable();
        GameObjectResourcePathResolved.Subscriber(Svc.PluginInterface, OnObjectPathResolved).Enable();
    }

    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(1);
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    public LocalPlayerData? LocalPlayerData { get; private set; }
    public ConcurrentDictionary<string, RemotePlayerData> RemotePlayerData { get; } = new();
    public Queue<string> PendingReads { get; } = new();
    public Queue<string> PendingApplies { get; } = new();
    
    public ConcurrentDictionary<string, List<string>> MissingPluginsByPlayerName { get; } = new();
    
    public void ReportMissingPlugin(string playerName, string pluginName)
    {
        if (!MissingPluginsByPlayerName.TryGetValue(playerName, out var missingPlugins))
            MissingPluginsByPlayerName[playerName] = missingPlugins = new();
        if (!missingPlugins.Contains(pluginName))
        {
            missingPlugins.Add(pluginName);
            Svc.Chat.PrintError($"[PSU] {playerName} is sending data that requires the {pluginName} plugin, which you do not have installed. Install it to see them as they see themselves!");
        }
        MissingPluginsByPlayerName[playerName] = missingPlugins;
    }

    private readonly List<ConditionFlag> _badConditions =
    [
        ConditionFlag.BeingMoved,
        ConditionFlag.BetweenAreas,
        ConditionFlag.BetweenAreas51,
        ConditionFlag.InCombat,
        ConditionFlag.Casting,
        ConditionFlag.Casting87,
        ConditionFlag.LoggingOut,
        ConditionFlag.Performing
    ];

    public void LocalUpdate(IFramework framework)
    {
        if (DateTime.Now - LastUpdated < TimeSpan.FromSeconds(PsuPlugin.Configuration.LocalPollingSeconds)) return;
        LastUpdated = DateTime.Now;

        if (!PsuPlugin.Configuration.SetupComplete)
            return;
        if (!Svc.ClientState.IsLoggedIn)
            return;

        if (Svc.ClientState.LocalPlayer == null || PsuPlugin.Configuration.MyStarPack == null ||
            Svc.ClientState.LocalPlayer.Address == IntPtr.Zero ||
            Svc.Condition.AsReadOnlySet().Any(x => _badConditions.Contains(x)))
            return;
        if (!GenericHelpers.IsScreenReady())
            return;
        LocalPlayerData ??= new LocalPlayerData(PsuPlugin.Configuration.MyStarPack);
        LocalPlayerData.Player = Svc.ClientState.LocalPlayer; // refresh before writing

        // Fire-and-forget local writes (disk IO off-thread)
        LocalPlayerData.UpdateBasicData();
        LocalPlayerData.UpdateGlamData();
        LocalPlayerData.UpdatePenumbraData();
        LocalPlayerData.UpdateCustomizeData();
        LocalPlayerData.UpdateHonorificData();
        LocalPlayerData.UpdateMoodlesData();
        LocalPlayerData.UpdateHeelsData();

        Task.Run(PsuPlugin.SyncThingService.CleanLocalDataPack);
    }

    private void RemoteUpdate(IFramework framework)
    {
        var nearbyPlayers = Svc.Objects.PlayerObjects.Cast<IPlayerCharacter>();
        foreach (var star in PsuPlugin.Configuration.StarPacks)
        {
            if (!RemotePlayerData.TryGetValue(star.StarId, out var remote))
                RemotePlayerData[star.StarId] = remote = new RemotePlayerData(star);
            
            var rates = PsuPlugin.SyncThingService.GetTransferRates(remote.StarPackReference.StarId);
            bool syncing = rates is { InBps: > 100 };
            if (syncing)
            {
                //Svc.Log.Debug($"[DEBUG] Syncing {star.StarId} - {rates?.InBps} Bps");
                continue;
            }

            if (remote.Data == null || DateTime.Now - remote.LastUpdated < TimeSpan.FromSeconds(PsuPlugin.Configuration.RemotePollingSeconds))
                PendingReads.Enqueue(star.StarId);

            remote.Player = nearbyPlayers.FirstOrDefault(p =>
                p.HomeWorld.RowId == remote.Data?.WorldId && p.Name.TextValue == remote.Data?.PlayerName);
            if (remote.Player == null && remote.AssignedCollectionId != null)
            {
                try
                {
                    var collId = remote.AssignedCollectionId.Value;
                    var penId = remote.PenumbraData?.Id ?? Guid.Empty;
                    if (penId != Guid.Empty)
                    {
                        var metaModName = $"PSU_Meta_{penId}";
                        var fileModName = $"PSU_File_{penId}";
                        PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(metaModName, collId, 0);
                        PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(fileModName, collId, 0);
                    }

                    PsuPlugin.PenumbraService.DeleteTemporaryCollection.Invoke(collId);
                }
                catch (Exception ex)
                {
                    Svc.Log.Error($"Error cleaning up temporary Penumbra collection (proactive): {ex}");
                }
                finally
                {
                    remote.AssignedCollectionId = null;
                    // Clear cached transient data so next appearance triggers full re-apply
                    remote.PenumbraData = null;
                    remote.GlamourerData = null;
                    remote.CustomizeData = null;
                    remote.HonorificData = null;
                    remote.MoodlesData = null;
                }
            }
            else if (remote.Player != null && remote.AssignedCollectionId == null)
            {
                PendingApplies.Enqueue(star.StarId);
            }
        }

        if (PendingReads.TryDequeue(out var starIdToRead))
        {
            Task.Run(() =>
            {
                try
                {
                    var remoteData = RemotePlayerData[starIdToRead];
                    var dataPack = remoteData.StarPackReference.GetDataPack();
                    if (dataPack == null)
                    {
                        Svc.Log.Warning($"DataPack for star {starIdToRead} is null, skipping read");
                        return;
                    }

                    var basePath = dataPack.DataPath;
                    var newBasic = BasicData.LoadFromDisk(basePath);
                    var newPenumbra = PenumbraData.LoadFromDisk(basePath);
                    var newGlamourer = GlamourerData.LoadFromDisk(basePath);
                    var newCustomize = CustomizeData.LoadFromDisk(basePath);
                    var newHonorific = HonorificData.LoadFromDisk(basePath);
                    var newMoodles = MoodlesData.LoadFromDisk(basePath);
                    var newHeels = HeelsData.LoadFromDisk(basePath);
                    bool dataChanged = false;
                    if (newBasic != null && !newBasic.Equals(remoteData.Data))
                    {
                        remoteData.Data = newBasic;
                        dataChanged = true;
                        Svc.Log.Debug($"[DEBUG] Basic data changed for {starIdToRead}");
                    }

                    if (newPenumbra != null && !newPenumbra.Equals(remoteData.PenumbraData))
                    {
                        remoteData.PenumbraData = newPenumbra;
                        dataChanged = true;
                        var filesPath = remoteData.StarPackReference.GetDataPack()?.FilesPath;
                        if (filesPath == null)
                        {
                            Svc.Log.Warning(
                                $"DataPack for star {starIdToRead} has null FilesPath, cannot prepare Penumbra paths");
                            return;
                        }

                        remoteData.PenumbraData.PreparePaths(filesPath, dataPack.Name);
                        Svc.Log.Debug($"[DEBUG] Penumbra data changed for {starIdToRead}");
                    }

                    if (newGlamourer != null && !newGlamourer.Equals(remoteData.GlamourerData))
                    {
                        remoteData.GlamourerData = newGlamourer;
                        dataChanged = true;
                        Svc.Log.Debug($"[DEBUG] Glamourer data changed for {starIdToRead}");
                    }

                    if (newCustomize != null && !newCustomize.Equals(remoteData.CustomizeData))
                    {
                        remoteData.CustomizeData = newCustomize;
                        dataChanged = true;
                        Svc.Log.Debug($"[DEBUG] Customize data changed for {starIdToRead}");
                    }

                    if (newHonorific != null && !newHonorific.Equals(remoteData.HonorificData))
                    {
                        remoteData.HonorificData = newHonorific;
                        dataChanged = true;
                        Svc.Log.Debug($"[DEBUG] Honorific data changed for {starIdToRead}");
                    }

                    if (newMoodles != null && !newMoodles.Equals(remoteData.MoodlesData))
                    {
                        remoteData.MoodlesData = newMoodles;
                        dataChanged = true;
                        Svc.Log.Debug($"[DEBUG] Moodles data changed for {starIdToRead}");
                    }

                    if (newHeels != null && !newHeels.Equals(remoteData.HeelsData))
                    {
                        remoteData.HeelsData = newHeels;
                        dataChanged = true;
                        Svc.Log.Debug($"[DEBUG] Heels data changed for {starIdToRead}");
                    }

                    if (dataChanged)
                    {
                        PendingApplies.Enqueue(starIdToRead);
                    }
                }
                catch (Exception ex)
                {
                    Svc.Log.Error($"Error reading remote player data: {ex}");
                }
            });
        }

        if (PendingApplies.TryDequeue(out var starIdToApply))
        {
            var remote = RemotePlayerData[starIdToApply];
            if (remote.Player == null)
                return;
            var basicApply = remote.Data?.ApplyData(remote.Player);
            var glamApply = remote.GlamourerData?.ApplyData(remote.Player);
            var penApply = remote.PenumbraData?.ApplyData(remote.Player, remote.AssignedCollectionId);
            var customizeApply = remote.CustomizeData?.ApplyData(remote.Player);
            var honorificApply = remote.HonorificData?.ApplyData(remote.Player);
            var moodlesApply = remote.MoodlesData?.ApplyData(remote.Player);
            var heelsApply = remote.HeelsData?.ApplyData(remote.Player);
            if (penApply is not { Applied: true }) return;
            var collectionId = Guid.Parse(penApply.Value.Result);
            remote.AssignedCollectionId = collectionId;
            remote.LastUpdated = DateTime.UtcNow;
        }
    }

    private void OnObjectPathResolved(nint gameObject, string gamePath, string localPath)
    {
        var realLocalPath = localPath.Split('|').Last();
        if (LocalPlayerData?.Player?.Address == gameObject)
        {
            LocalPlayerData.UpdateTransientData(gamePath, realLocalPath);
        }
    }

    public void Dispose()
    {
        Svc.Framework.Update -= LocalUpdate;
        Svc.ClientState.Logout -= OnLogout;
        //StateChanged.Subscriber(Svc.PluginInterface, OnGlamourerStateChanged).Disable();
        GameObjectResourcePathResolved.Subscriber(Svc.PluginInterface, OnObjectPathResolved).Disable();
    }

    private void OnLogout(int a, int b)
    {
        // Best-effort cleanup of any temporary collections/mods
        try
        {
            foreach (var remote in RemotePlayerData)
            {
                if (remote.Value.AssignedCollectionId != null)
                {
                    var collId = remote.Value.AssignedCollectionId.Value;
                    var penId = remote.Value.PenumbraData?.Id ?? Guid.Empty;
                    if (penId != Guid.Empty)
                    {
                        var metaModName = $"PSU_Meta_{penId}";
                        var fileModName = $"PSU_File_{penId}";
                        PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(metaModName, collId, 0);
                        PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(fileModName, collId, 0);
                    }

                    PsuPlugin.PenumbraService.DeleteTemporaryCollection.Invoke(collId);
                    remote.Value.AssignedCollectionId = null;
                    // Clear cached transient data so next appearance triggers full re-apply
                    remote.Value.PenumbraData = null;
                    remote.Value.GlamourerData = null;
                    remote.Value.CustomizeData = null;
                    remote.Value.HonorificData = null;
                    remote.Value.MoodlesData = null;
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error cleaning up Penumbra temporary collections on logout: {ex}");
        }
    }
}