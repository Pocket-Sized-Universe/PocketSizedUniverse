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
    private sealed class RemoteApplySnapshot
    {
        public BasicData Basic { get; init; }
        public GlamourerData Glamourer { get; init; }
        public PenumbraData Penumbra { get; init; }
        public CustomizeData Customize { get; init; }
        public HonorificData Honorific { get; init; }
        public MoodlesData Moodles { get; init; }
    }

    private readonly ConcurrentDictionary<string, RemoteApplySnapshot> _pendingApplies = new();
    private readonly ConcurrentDictionary<string, byte> _readsInFlight = new();
    private const int MaxAppliesPerTick = 1;

    private DateTime _applyNotBeforeUtc = DateTime.MaxValue;
    private static readonly TimeSpan LoginDelay = TimeSpan.FromSeconds(15);

    public PlayerDataService()
    {
        Svc.Framework.Update += Update;
        Svc.ClientState.Login += OnLogin;
        Svc.ClientState.Logout += OnLogout;
        StateChanged.Subscriber(Svc.PluginInterface, OnGlamourerStateChanged).Enable();
        //GameObjectRedrawn.Subscriber(Svc.PluginInterface, OnRedraw).Enable();
        GameObjectResourcePathResolved.Subscriber(Svc.PluginInterface, OnObjectPathResolved).Enable();
        if (Svc.ClientState.IsLoggedIn)
            OnLogin();
    }

    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(10);
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    public LocalPlayerData? LocalPlayerData { get; private set; }
    public ConcurrentBag<RemotePlayerData> RemotePlayerData { get; } = new();

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

    public void Update(IFramework framework)
    {
        if (DateTime.Now - LastUpdated < UpdateInterval) return;
        LastUpdated = DateTime.Now;

        if (!PsuPlugin.Configuration.SetupComplete)
            return;
        if (!Svc.ClientState.IsLoggedIn)
            return;

        // Delay all processing shortly after login to avoid applying during world load
        if (DateTime.UtcNow < _applyNotBeforeUtc)
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
        // Task.Run(LocalPlayerData.UpdatePenumbraData);
        LocalPlayerData.UpdateCustomizeData();
        LocalPlayerData.UpdateHonorificData();
        LocalPlayerData.UpdateMoodlesData();

        foreach (var star in PsuPlugin.Configuration.StarPacks)
        {
            if (RemotePlayerData.Any(x => x.StarPackReference.StarId == star.StarId))
                continue;
            RemotePlayerData.Add(new RemotePlayerData(star));
        }

        // Proactive cleanup for any remote with assigned collection but no player
        foreach (var remote in RemotePlayerData)
        {
            if (remote.AssignedCollectionId != null)
            {
                // Check current player presence based on last known BasicData
                var players = Svc.Objects.PlayerObjects.Cast<IPlayerCharacter>();
                var known = remote.Data;
                var player = known == null
                    ? null
                    : players.FirstOrDefault(p =>
                        p.HomeWorld.RowId == known.WorldId && p.Name.TextValue == known.PlayerName);
                if (player == null)
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
            }
        }

        // Stage remote snapshots in background; apply on main thread when ready
        foreach (var remote in RemotePlayerData)
        {
            try
            {
                var dataPack = remote.StarPackReference.GetDataPack();
                if (dataPack == null)
                    continue;

                var starId = remote.StarPackReference.StarId;
                if (_pendingApplies.ContainsKey(starId) || _readsInFlight.ContainsKey(starId))
                    continue;

                _readsInFlight.TryAdd(starId, 1);
                Task.Run(() =>
                {
                    try
                    {
                        var basePath = dataPack.DataPath;
                        var newBasic = BasicData.LoadFromDisk(basePath);
                        var newPenumbra = PenumbraData.LoadFromDisk(basePath);
                        var newGlamourer = GlamourerData.LoadFromDisk(basePath);
                        var newCustomize = CustomizeData.LoadFromDisk(basePath);
                        var newHonorific = HonorificData.LoadFromDisk(basePath);
                        var newMoodles = MoodlesData.LoadFromDisk(basePath);

                        if (newBasic == null || newPenumbra == null || newGlamourer == null || newCustomize == null ||
                            newHonorific == null || newMoodles == null)
                        {
                            return;
                        }

                        // Precompute Penumbra mappings off-thread; avoid IO on main thread
                        try
                        {
                            var paths = new Dictionary<string, string>();
                            var filesPath = dataPack.FilesPath;
                            foreach (var customFile in newPenumbra.Files)
                            {
                                var localFilePath = customFile.GetPath(filesPath);
                                if (!File.Exists(localFilePath))
                                    continue;
                                foreach (var gamePath in customFile.ApplicableGamePaths)
                                {
                                    if (string.IsNullOrWhiteSpace(gamePath)) continue;
                                    paths[gamePath] = localFilePath;
                                }
                            }

                            foreach (var swap in newPenumbra.FileSwaps)
                            {
                                if (swap.GamePath != null)
                                    paths[swap.GamePath] = swap.RealPath;
                            }

                            newPenumbra.PreparedPaths = paths;
                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Error($"Error preparing Penumbra mapping: {ex}");
                        }

                        var snapshot = new RemoteApplySnapshot
                        {
                            Basic = newBasic,
                            Glamourer = newGlamourer,
                            Penumbra = newPenumbra,
                            Customize = newCustomize,
                            Honorific = newHonorific,
                            Moodles = newMoodles
                        };
                        _pendingApplies[starId] = snapshot;
                    }
                    catch (Exception ex)
                    {
                        Svc.Log.Error($"Error reading remote player data: {ex}");
                    }
                    finally
                    {
                        _readsInFlight.TryRemove(starId, out _);
                    }
                });
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Error scheduling remote player data read: {ex}");
            }
        }

        // Apply at most N snapshots per tick on main thread (avoid freeze)
        int applied = 0;
        foreach (var kv in _pendingApplies.ToArray())
        {
            if (applied >= MaxAppliesPerTick)
                break;

            if (!_pendingApplies.TryRemove(kv.Key, out var snap))
                continue;

            var remote = RemotePlayerData.FirstOrDefault(r => r.StarPackReference.StarId == kv.Key);
            if (remote == null)
                continue;

            // Refresh the player reference just before apply (avoid stale)
            var players = Svc.Objects.PlayerObjects.Cast<IPlayerCharacter>();
            remote.Player = players.FirstOrDefault(p =>
                p.HomeWorld.RowId == snap.Basic.WorldId && p.Name.TextValue == snap.Basic.PlayerName);

            // If player is not present but we have an assigned Penumbra collection, clean it up.
            if (remote.Player == null && remote.AssignedCollectionId != null)
            {
                try
                {
                    var collId = remote.AssignedCollectionId.Value;
                    // Prefer snapshot ID; fallback to previously applied data
                    var penId = snap.Penumbra?.Id ?? remote.PenumbraData?.Id ?? Guid.Empty;
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
                    Svc.Log.Error($"Error cleaning up temporary Penumbra collection: {ex}");
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

                // Skip applying when player isn't present
                continue;
            }

            // Apply in dependency-friendly order
            snap.Basic.ApplyData(remote);
            snap.Glamourer.ApplyData(remote);
            snap.Penumbra.ApplyData(remote);
            snap.Customize.ApplyData(remote);
            snap.Honorific.ApplyData(remote);
            snap.Moodles.ApplyData(remote);

            applied++;
        }
    }

    private DateTime _lastResolve = DateTime.MinValue;

    private void OnObjectPathResolved(nint gameObject, string gamePath, string localPath)
    {
        if (DateTime.Now - _lastResolve < TimeSpan.FromSeconds(1)) return;
        _lastResolve = DateTime.Now;

        if (!PsuPlugin.Configuration.SetupComplete)
        {
            Svc.Log.Warning("Resolved path before setup complete.");
            return;
        }

        var localPlayer = Svc.Framework.RunOnFrameworkThread(() => Svc.ClientState.LocalPlayer).Result;
        if (!Svc.ClientState.IsLoggedIn || localPlayer == null ||
            localPlayer.Address == IntPtr.Zero || PsuPlugin.Configuration.MyStarPack == null)
            return;
        LocalPlayerData ??= new LocalPlayerData(PsuPlugin.Configuration.MyStarPack);
        LocalPlayerData.Player = Svc.Framework.RunOnFrameworkThread(() => Svc.ClientState.LocalPlayer).Result;
        // Run on framework thread; UpdatePenumbraData offloads heavy work internally
        LocalPlayerData.UpdatePenumbraData();
    }

    private void OnGlamourerStateChanged(IntPtr objPointer)
    {
        var obj = Svc.Objects.CreateObjectReference(objPointer);
        if (obj == null)
        {
            Svc.Log.Debug("Glamourer changed object not in object table");
            return;
        }

        if (Player.Object.Address != obj.Address)
        {
            Svc.Log.Debug("Glamourer changed object not for local player");
            return;
        }

        Svc.Log.Debug("Glamourer state changed");
        if (LocalPlayerData is null)
        {
            Svc.Log.Warning("Glamourer state trigger with no local player data.");
            return;
        }

        if (!Svc.ClientState.IsLoggedIn || Svc.ClientState.LocalPlayer == null ||
            Svc.ClientState.LocalPlayer.Address == IntPtr.Zero || PsuPlugin.Configuration.MyStarPack == null)
            return;
        LocalPlayerData.Player = Svc.ClientState.LocalPlayer;
        Task.Run(LocalPlayerData.UpdateGlamData);
    }

    public void Dispose()
    {
        Svc.Framework.Update -= Update;
        Svc.ClientState.Login -= OnLogin;
        Svc.ClientState.Logout -= OnLogout;
        //StateChanged.Subscriber(Svc.PluginInterface, OnGlamourerStateChanged).Disable();
        //GameObjectResourcePathResolved.Subscriber(Svc.PluginInterface, OnObjectPathResolved).Disable();
    }

    private void OnLogin()
    {
        _applyNotBeforeUtc = DateTime.UtcNow + LoginDelay;
    }

    private void OnLogout(int a, int b)
    {
        _applyNotBeforeUtc = DateTime.MaxValue; // block until next login sets it

        // Best-effort cleanup of any temporary collections/mods
        try
        {
            foreach (var remote in RemotePlayerData)
            {
                if (remote.AssignedCollectionId != null)
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
                    remote.AssignedCollectionId = null;
                    // Clear cached transient data so next appearance triggers full re-apply
                    remote.PenumbraData = null;
                    remote.GlamourerData = null;
                    remote.CustomizeData = null;
                    remote.HonorificData = null;
                    remote.MoodlesData = null;
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error cleaning up Penumbra temporary collections on logout: {ex}");
        }

        _pendingApplies.Clear();
        _readsInFlight.Clear();
    }
}