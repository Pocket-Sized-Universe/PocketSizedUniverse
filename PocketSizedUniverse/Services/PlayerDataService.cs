using System.Collections.Concurrent;
using System.Security.Cryptography;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Glamourer.Api.Enums;
using Penumbra.Api.Enums;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Models.Data;
using PocketSizedUniverse.Models.Mods;

namespace PocketSizedUniverse.Services;

public class PlayerDataService : IUpdatable
{
    public PlayerDataService()
    {
        Svc.Framework.Update += Update;
    }

    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(30);
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    public PlayerData? LocalPlayerData { get; private set; }
    public ConcurrentDictionary<StarPack, (Guid CollectionId, PlayerData PlayerData)> RemotePlayerData { get; } = new();

    public void Update(IFramework framework)
    {
        if (DateTime.Now - LastUpdated < UpdateInterval) return;
        LastUpdated = DateTime.Now;

        // Run the data push in the background
        Task.Run(PushLocalPlayerDataAsync);
        Task.Run(UpdateRemotePlayerDataAsync);
    }

    private async Task PushLocalPlayerDataAsync()
    {
        try
        {
            if (!PsuPlugin.Configuration.SetupComplete)
                return;


            var pc = Svc.Framework.RunOnFrameworkThread(() => Player.Object).Result;
            if (pc == null)
            {
                Svc.Log.Warning("Local player object is null.");
                return;
            }

            LocalPlayerData ??= new PlayerData(PsuPlugin.Configuration.MyStarPack!);
            await LocalPlayerData.PopulateFromLocalAsync(pc);
            await LocalPlayerData.SavePlayerDataToDiskAsync();

            Svc.Log.Debug("Completed local player data update cycle.");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error in PushLocalPlayerDataAsync: {ex}");
        }
    }

    private async Task UpdateRemotePlayerDataAsync()
    {
        try
        {
            foreach (var pair in PsuPlugin.Configuration.StarPacks)
            {
                var remoteStar = PsuPlugin.SyncThingService.Stars.Values.FirstOrDefault(s => s.StarId == pair.StarId);
                if (remoteStar == null)
                {
                    Svc.Log.Warning($"Remote star {pair.StarId} was null while updating remote player data.");
                    continue;
                }

                var remotePack = PsuPlugin.SyncThingService.DataPacks
                    .FirstOrDefault(dp => dp.Value.Id == pair.DataPackId)
                    .Value;
                if (remotePack == null)
                {
                    Svc.Log.Warning($"Remote data pack {pair.DataPackId} was null while updating remote player data.");
                    continue;
                }

                Svc.Log.Debug($"Updating remote player data for {remoteStar.StarId}");
                if (!RemotePlayerData.TryGetValue(pair, out var remoteData))
                {
                    Svc.Log.Debug($"Creating new remote player data for {remoteStar.StarId}");
                    await Svc.Framework.RunOnFrameworkThread(() =>
                    {
                        try
                        {
                            PsuPlugin.PenumbraService.CreateTemporaryCollection.Invoke(remoteStar.StarId,
                                remoteStar.Name, out var collectionId);
                            var playerData = new PlayerData(pair);
                            RemotePlayerData.TryAdd(pair, (collectionId, playerData));
                            Svc.Log.Debug(
                                $"Created new remote player data for {remoteStar.StarId} | CollectionId: {collectionId}");
                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Error($"Failed to create remote player data for {remoteStar.StarId}: {ex}");
                        }
                    });
                    continue;
                }

                await remoteData.PlayerData.PopulateFromDiskAsync();
                await Svc.Framework.RunOnFrameworkThread(() =>
                {
                    var remotePlayer = Svc.Objects.PlayerObjects.FirstOrDefault(p => p.Name.TextValue == remoteData.PlayerData.Data.PlayerName);
                    if (remotePlayer == null)
                    {
                        Svc.Log.Debug($"{remoteData.PlayerData.StarPackReference.StarId} is not nearby.");
                        return;
                    }

                    PsuPlugin.GlamourerService.ApplyState.Invoke(remoteData.PlayerData.GlamourerData.GlamState,
                        remotePlayer.ObjectIndex);
                    foreach (var mod in remoteData.PlayerData.PenumbraData.Mods)
                    {
                        var paths = new Dictionary<string, string>();
                        foreach (var customFile in mod.CustomFiles)
                        {
                            foreach (var realPath in customFile.ApplicableGamePaths)
                                paths.Add(realPath, customFile.GetPath(remotePack.FilesPath));
                        }

                        PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(
                            remoteData.PlayerData.PenumbraData.Id.ToString(), remoteData.CollectionId, paths,
                            remoteData.PlayerData.PenumbraData.MetaManipulations, mod.Priority);
                        IReadOnlyDictionary<string, IReadOnlyList<string>> settings = mod.Settings.ToDictionary<KeyValuePair<string, List<string>>, string, IReadOnlyList<string>>(s => s.Key, s => s.Value);
                        PsuPlugin.PenumbraService.SetTemporaryModSettings.Invoke(remoteData.CollectionId,
                            remotePack.FilesPath, mod.Inherited, mod.Enabled, mod.Priority, settings, "PSU");
                    }

                    foreach (var swap in remoteData.PlayerData.PenumbraData.AssetSwaps)
                    {
                        var paths = new Dictionary<string, string>();
                        if (swap.GamePath != null)
                            paths.Add(swap.RealPath, swap.GamePath);
                        PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(
                            remoteData.PlayerData.PenumbraData.Id.ToString(), remoteData.CollectionId, paths,
                            remoteData.PlayerData.PenumbraData.MetaManipulations, 1);
                    }
                    PsuPlugin.PenumbraService.AssignTemporaryCollection.Invoke(remoteData.CollectionId, remotePlayer.ObjectIndex);
                    PsuPlugin.PenumbraService.RedrawObject.Invoke(remotePlayer.ObjectIndex);
                });
            }

            Svc.Log.Debug("Updated remote player data.");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error updating remote player data: {ex}");
        }
    }
}