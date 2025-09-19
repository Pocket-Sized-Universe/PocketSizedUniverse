using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.EzIpcManager;
using ECommons.GameHelpers;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Models.Data;
using PocketSizedUniverse.Models.Mods;

namespace PocketSizedUniverse.Services;

public class PlayerDataService : IUpdatable
{
    private readonly ConcurrentDictionary<StarPack, DateTime> _remoteLastSeen = new();

    public PlayerDataService()
    {
        Svc.Framework.Update += Update;
        Svc.ClientState.Login += OnLogin;
        StateChanged.Subscriber(Svc.PluginInterface, OnGlamourerStateChanged).Enable();
        //GameObjectRedrawn.Subscriber(Svc.PluginInterface, OnRedraw).Enable();
        GameObjectResourcePathResolved.Subscriber(Svc.PluginInterface, OnObjectPathResolved).Enable();
        if (Svc.ClientState.IsLoggedIn)
            OnLogin();
    }

    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(5);
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    public LocalPlayerData? LocalPlayerData { get; private set; }
    public ConcurrentBag<RemotePlayerData> RemotePlayerData { get; } = new();

    public void Update(IFramework framework)
    {
        if (DateTime.Now - LastUpdated < UpdateInterval) return;
        LastUpdated = DateTime.Now;

        if (!PsuPlugin.Configuration.SetupComplete)
            return;
        LocalPlayerData ??= new LocalPlayerData(PsuPlugin.Configuration.MyStarPack!);
        Svc.Log.Debug("Updating local player data");
        Task.Run(LocalPlayerData.UpdateCustomizeData);
        Task.Run(LocalPlayerData.UpdateHonorificData);

        foreach (var star in PsuPlugin.Configuration.StarPacks)
        {
            if (RemotePlayerData.Any(x => x.StarPackReference.StarId == star.StarId))
                continue;
            RemotePlayerData.Add(new RemotePlayerData(star));
        }

        // Periodically sync remote player data from disk and apply if changed
        foreach (var remote in RemotePlayerData)
        {
            try
            {
                var dataPack = remote.StarPackReference.GetDataPack();
                if (dataPack == null)
                    continue;

                var newBasic = BasicData.LoadFromDisk(dataPack.DataPath);
                var newPenumbra = PenumbraData.LoadFromDisk(dataPack.DataPath);
                var newGlamourer = GlamourerData.LoadFromDisk(dataPack.DataPath);
                var newCustomize = CustomizeData.LoadFromDisk(dataPack.DataPath);
                var newHonorific = HonorificData.LoadFromDisk(dataPack.DataPath);
                if (newBasic == null || newPenumbra == null || newGlamourer == null || newCustomize == null || newHonorific == null)
                {
                    Svc.Log.Debug($"Data incomplete for {remote.StarPackReference.StarId}");
                    continue;
                }

                if (remote.Player == null)
                {
                    var players = Svc.Objects.PlayerObjects.Cast<IPlayerCharacter>();
                    var remotePlayer = players.FirstOrDefault(p =>
                        p.HomeWorld.RowId == newBasic.WorldId && p.Name.TextValue == newBasic.PlayerName);
                    if (remotePlayer != null)
                        remote.Player = remotePlayer;
                    else
                    {
                        Svc.Log.Debug($"Player not found for {remote.StarPackReference.StarId}");
                        continue;
                    }
                }

                remote.ApplyBasicIfChanged(newBasic);
                remote.ApplyGlamourerIfChanged(newGlamourer);
                remote.ApplyPenumbraIfChanged(newPenumbra);
                remote.ApplyCustomzieIfChanged(newCustomize);
                remote.ApplyHonorificIfChanged(newHonorific);
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Error updating remote player data: {ex}");
            }
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

        LocalPlayerData ??= new LocalPlayerData(PsuPlugin.Configuration.MyStarPack!);
        Task.Run(LocalPlayerData.UpdatePenumbraData);
    }

    private void OnLogin()
    {
        if (!PsuPlugin.Configuration.SetupComplete)
        {
            Svc.Log.Warning("Login triggered before setup complete.");
            return;
        }

        LocalPlayerData ??= new LocalPlayerData(PsuPlugin.Configuration.MyStarPack!);
        Task.Run(LocalPlayerData.UpdateBasicData);
        Task.Run(LocalPlayerData.UpdateGlamData);
        Task.Run(LocalPlayerData.UpdatePenumbraData);
        Task.Run(LocalPlayerData.UpdateCustomizeData);
    }

    private void OnRedraw(IntPtr objPointer, int index)
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

        Task.Run(LocalPlayerData.UpdatePenumbraData);
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

        Task.Run(LocalPlayerData.UpdateGlamData);
        //Task.Run(LocalPlayerData.UpdatePenumbraData);
    }
}