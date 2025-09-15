using System.Collections.Concurrent;
using System.Collections.ObjectModel;
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
    public ConcurrentDictionary<StarPack, (Guid? CollectionId, PlayerData PlayerData)> RemotePlayerData { get; } = new();

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
                            var playerData = new PlayerData(pair);
                            RemotePlayerData.TryAdd(pair, (null, playerData));
                            Svc.Log.Debug(
                                $"Created new remote player data for {remoteStar.StarId}");
                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Error($"Failed to create remote player data for {remoteStar.StarId}: {ex}");
                        }
                    });
                    continue;
                }

                // Validate collection still exists before proceeding
                Svc.Log.Debug($"Using existing collection {remoteData.CollectionId} for {remoteStar.StarId}");

                try
                {
                    await remoteData.PlayerData.PopulateFromDiskAsync();
                    await Svc.Framework.RunOnFrameworkThread(() =>
                    {
                        var remotePlayer = Svc.Objects.PlayerObjects.FirstOrDefault(p =>
                            p.Name.TextValue == remoteData.PlayerData.Data.PlayerName);
                        if (remotePlayer == null)
                        {
                            Svc.Log.Debug($"{remoteData.PlayerData.Data.PlayerName} is not nearby.");
                            return;
                        }

                        if (remoteData.CollectionId == null)
                        {
                            Svc.Log.Debug("Creating new collection for {remoteStar.StarId}");
                            PsuPlugin.PenumbraService.CreateTemporaryCollection.Invoke("PocketSizedUniverse", remoteData.PlayerData.Data.Id.ToString(), out var newCollectionId);
                            remoteData.CollectionId = newCollectionId;
                        }

                        PsuPlugin.GlamourerService.ApplyState.Invoke(remoteData.PlayerData.GlamourerData.GlamState,
                            remotePlayer.ObjectIndex);

                        // Apply meta manipulations first
                        var metaModName = $"PSU_Meta_{remoteData.PlayerData.PenumbraData.Id}";
                        Svc.Log.Debug($"Removing existing meta mod {metaModName} from collection {remoteData.CollectionId}");
                        PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(metaModName, remoteData.CollectionId.Value, 0);
                        
                        if (!string.IsNullOrEmpty(remoteData.PlayerData.PenumbraData.MetaManipulations))
                        {
                            Svc.Log.Debug($"Adding meta mod {metaModName} to collection {remoteData.CollectionId}");
                            PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(
                                metaModName, remoteData.CollectionId.Value, new Dictionary<string, string>(),
                                remoteData.PlayerData.PenumbraData.MetaManipulations, 0);
                        }

                        // Apply file mods (like the working prototype) - each CustomFile becomes a separate mod
                        int modIndex = 0;
                        foreach (var mod in remoteData.PlayerData.PenumbraData.Mods)
                        {
                            var paths = new Dictionary<string, string>();
                            foreach (var customFile in mod.CustomFiles)
                            {
                                var localFilePath = customFile.GetPath(remotePack.FilesPath);
                                
                                // Check if the file actually exists
                                if (!File.Exists(localFilePath))
                                {
                                    Svc.Log.Warning($"Custom file does not exist: {localFilePath}");
                                    continue;
                                }
                                
                                // Create one mod per CustomFile (like the working prototype)
                                foreach (var gamePath in customFile.ApplicableGamePaths)
                                {
                                    paths[gamePath] = localFilePath;
                                }


                                modIndex++;
                            }
                            foreach (var assetSwap in mod.AssetSwaps)
                            {
                                if (assetSwap.GamePath != null)
                                    paths.Add(assetSwap.RealPath, assetSwap.GamePath);
                                PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(
                                    $"PSU_AssetSwap_{remoteData.PlayerData.PenumbraData.Id}_{mod.Priority}_{modIndex}", remoteData.CollectionId.Value, paths,
                                    string.Empty, mod.Priority);
                            }
                            if (paths.Count > 0)
                            {
                                var fileModName = $"PSU_File_{remoteData.PlayerData.PenumbraData.Id}_{mod.Priority}_{modIndex}";
                                PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(fileModName, remoteData.CollectionId.Value, mod.Priority);
                                PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(
                                    fileModName, remoteData.CollectionId.Value, paths, string.Empty, mod.Priority);
                            }
                        }

                        PsuPlugin.PenumbraService.AssignTemporaryCollection.Invoke(remoteData.CollectionId.Value,
                            remotePlayer.ObjectIndex);
                        PsuPlugin.PenumbraService.RedrawObject.Invoke(remotePlayer.ObjectIndex);
                    });
                }
                catch (Exception ex)
                {
                    Svc.Log.Error($"Failed to update remote player data for {remoteStar.StarId}: {ex}");
                }
            }

            Svc.Log.Debug("Updated remote player data.");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error updating remote player data: {ex}");
        }
    }
}