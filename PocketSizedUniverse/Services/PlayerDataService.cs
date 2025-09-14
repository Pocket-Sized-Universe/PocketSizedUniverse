using System.Collections.Concurrent;
using System.Security.Cryptography;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Glamourer.Api.Enums;
using Penumbra.Api.Enums;
using PocketSizedUniverse.Interfaces;
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
    public ConcurrentBag<PlayerData> RemotePlayerData { get; } = new();
    public void Update(IFramework framework)
    {
        if (DateTime.Now - LastUpdated < UpdateInterval) return;
        LastUpdated = DateTime.Now;

        PushLocalPlayerData();
    }

    private void PushLocalPlayerData()
    {
        if (!PsuPlugin.Configuration.SetupComplete)
            return;
        var manips = PsuPlugin.PenumbraService.GetPlayerMetaManipulations.Invoke();
        var tree = PsuPlugin.PenumbraService.GetPlayerResourceTrees.Invoke().FirstOrDefault().Value;
        if (tree == null)
        {
            Svc.Log.Warning("Failed to get player resource tree.");
            return;
        }
        var penumbra = new PenumbraData();
        Svc.Log.Debug("Updating local player data.");
        Svc.Log.Debug("Node Count: " + tree.Nodes.Count);
        penumbra.MetaManipulations = manips;
        foreach (var node in tree.Nodes)
        {
            if (node.GamePath == null || node.GamePath == node.ActualPath)
            {
                Svc.Log.Debug("Skipping identical or non-existent item: " + node.ActualPath);
                continue;
            }
            var myDataPack = PsuPlugin.Configuration.MyStarPack?.GetDataPack();
            if (myDataPack == null)
            {
                Svc.Log.Warning("MyDataPack was null while updating local player data.");
                continue;
            }
            var filesBase = myDataPack.FilesPath;
            if (File.Exists(node.ActualPath))
            {
                Svc.Log.Debug("Handling custom file: " + node.ActualPath);
                var data = File.ReadAllBytes(node.ActualPath);
                var hash = SHA256.Create().ComputeHash(data);
                var redirect = new CustomRedirect(node.GamePath, hash);
                if (!File.Exists(redirect.GetPath(filesBase)))
                {
                    Svc.Log.Debug("Writing custom file: " + redirect.GetPath(filesBase));
                    File.WriteAllBytes(redirect.GetPath(filesBase), data);
                    Svc.Log.Debug("Wrote custom file: " + redirect.GetPath(filesBase));
                }
                else
                {
                    Svc.Log.Debug("Custom file already exists: " + redirect.GetPath(filesBase));
                }
                penumbra.CustomFiles.Add(redirect);
            }
            else
            {
                Svc.Log.Debug("Handling asset redirect: " + node.ActualPath);
                var assetDef = new AssetSwap(node.GamePath, node.ActualPath);
                penumbra.AssetSwaps.Add(assetDef);
                Svc.Log.Debug("Handled asset redirect: " + node.ActualPath);
            }
        }

        var glamState = PsuPlugin.GlamourerService.GetStateBase64.Invoke(Player.Object.ObjectIndex);
        if (glamState.Item2 == null)
        {
            Svc.Log.Warning("Failed to get glamourer state.");
            return;
        }

        var glamData = new GlamourerData()
        {
            GlamState = glamState.Item2
        };
        if (LocalPlayerData == null)
        {
            LocalPlayerData = new PlayerData()
            {
                Data = new BasicData()
                {
                    PlayerName = Player.Name,
                    WorldId = Player.HomeWorldId
                },
                PenumbraData = penumbra,
                GlamourerData = glamData,
            };
        }
        else
        {
            LocalPlayerData.Data.PlayerName = Player.Name;
            LocalPlayerData.Data.WorldId = Player.HomeWorldId;
            LocalPlayerData.PenumbraData = penumbra;
        }
        var localStar = PsuPlugin.Configuration.MyStarPack?.GetStar();
        if (localStar == null)
        {
            Svc.Log.Warning("Local star was null while updating local player data.");
            return;
        }
        var localPack = PsuPlugin.Configuration.MyStarPack?.GetDataPack();
        if (localPack == null)
        {
            Svc.Log.Warning("Local data pack was null while updating local player data.");
            return;
        }
        LocalPlayerData.StarPackReference = PsuPlugin.Configuration.MyStarPack!;

        Svc.Log.Debug("Updating local player data on disk.");
        var playerDataLoc = LocalPlayerData.Data.GetPath(localPack.DataPath);
        var encodedData = Base64Util.ToBase64(LocalPlayerData.Data);
        File.WriteAllText(playerDataLoc, encodedData);
        Svc.Log.Debug("Updated basic info on disk.");
        var penumbraLoc = LocalPlayerData.PenumbraData.GetPath(localPack.DataPath);
        var encodedPenumbra = Base64Util.ToBase64(LocalPlayerData.PenumbraData);
        File.WriteAllText(penumbraLoc, encodedPenumbra);
        Svc.Log.Debug("Updated penumbra data on disk.");
        var glamourerLoc = LocalPlayerData.GlamourerData.GetPath(localPack.DataPath);
        var encodedGlamourer = Base64Util.ToBase64(LocalPlayerData.GlamourerData);
        File.WriteAllText(glamourerLoc, encodedGlamourer);
        Svc.Log.Debug("Updated glamourer data on disk.");
        Svc.Log.Debug("Updated local player data on disk.");
        RemotePlayerData.Clear();
        foreach (var pair in PsuPlugin.Configuration.StarPacks)
        {
            var remoteStar = PsuPlugin.SyncThingService.Stars.Values.FirstOrDefault(s => s.StarId == pair.StarId);
            if (remoteStar == null)
            {
                Svc.Log.Warning($"Remote star {pair.StarId} was null while updating remote player data.");
                continue;
            }
            var remotePack = PsuPlugin.SyncThingService.DataPacks.FirstOrDefault(dp => dp.Value.Id == pair.DataPackId).Value;
            if (remotePack == null)
            {
                Svc.Log.Warning($"Remote data pack {pair.DataPackId} was null while updating remote player data.");
                continue;
            }

            var data = PlayerData.FromDataPack(remotePack);
            if (data == null)
            {
                Svc.Log.Warning($"Failed to load data from data pack {pair.DataPackId}");
                continue;
            }
            data.StarPackReference = pair;
            Svc.Log.Debug($"Updating remote player data for {remoteStar.StarId}: {data.Data.PlayerName}");
            RemotePlayerData.Add(data);
        }
        Svc.Log.Debug("Updated remote player data.");
        foreach (var remotePlayer in RemotePlayerData)
        {
            var player = Svc.Objects.PlayerObjects.FirstOrDefault(p => p.Name.TextValue == remotePlayer.Data.PlayerName);
            if (player == null)
            {
                Svc.Log.Warning($"Failed to find player object for {remotePlayer.Data.PlayerName}");
                continue;
            }
            Svc.Log.Debug($"Remote player data: {remotePlayer.Data.PlayerName}");
            Svc.Log.Debug($"Remote player asset swaps: {remotePlayer.PenumbraData.AssetSwaps.Count}");
            Svc.Log.Debug($"Remote player custom files: {remotePlayer.PenumbraData.CustomFiles.Count}");
            Svc.Log.Debug($"Remote player meta manips: {remotePlayer.PenumbraData.MetaManipulations}");
            if (remotePlayer.PenumbraData.CollectionId == null)
            {
                Svc.Log.Debug("Remote player collection id is null.");
                PsuPlugin.PenumbraService.CreateTemporaryCollection.Invoke(remotePlayer.Data.PlayerName, $"PSU_{remotePlayer.PenumbraData.Id}", out var newColl);
                remotePlayer.PenumbraData.CollectionId = newColl;
                Svc.Log.Debug($"Created new collection: {newColl}");

                PsuPlugin.PenumbraService.AssignTemporaryCollection.Invoke(remotePlayer.PenumbraData.CollectionId.Value,
                    player.ObjectIndex);
                Svc.Log.Debug($"Assigned collection {remotePlayer.PenumbraData.CollectionId.Value} to player {player.Name.TextValue}");
            }
            Dictionary<string, string> paths = new Dictionary<string, string>();
            Svc.Log.Debug($"Processing {remotePlayer.PenumbraData.CustomFiles.Count} custom files.");
            var dataPack = remotePlayer.StarPackReference.GetDataPack();
            if (dataPack == null)
            {
                Svc.Log.Warning($"No data pack found for {remotePlayer.Data.PlayerName}");
                continue;
            }
            Svc.Log.Debug($"Data pack for {remotePlayer.Data.PlayerName}: {dataPack.Id}");
            foreach (var customFile in remotePlayer.PenumbraData.CustomFiles)
            {
                if (customFile.GamePath == null)
                    continue;
                Svc.Log.Debug($"Processing custom file: {customFile.GamePath}");
                var realPath = customFile.GetPath(dataPack.FilesPath);
                Svc.Log.Debug($"Real path for {customFile.GamePath}: {realPath}");
                paths[customFile.GamePath] = realPath;
            }
            Svc.Log.Debug($"Processing {remotePlayer.PenumbraData.AssetSwaps.Count} asset swaps.");
            foreach (var assetSwap in remotePlayer.PenumbraData.AssetSwaps)
            {
                Svc.Log.Debug($"Processing asset swap: {assetSwap.GamePath}");
                if (assetSwap.GamePath != null)
                    paths[assetSwap.GamePath] = assetSwap.RealPath;
            }

            Svc.Log.Debug($"Glam state for {remotePlayer.Data.PlayerName}: {remotePlayer.GlamourerData.GlamState}");
            PsuPlugin.GlamourerService.ApplyState.Invoke(remotePlayer.GlamourerData.GlamState, player.ObjectIndex, 42069, ApplyFlag.Customization | ApplyFlag.Equipment);
            Svc.Log.Debug($"Applied glamourer state for {remotePlayer.Data.PlayerName}");
            PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(remotePlayer.PenumbraData.Id.ToString(), remotePlayer.PenumbraData.CollectionId.Value, paths, remotePlayer.PenumbraData.MetaManipulations, 0);
            Svc.Log.Debug($"Added temporary mod for {remotePlayer.Data.PlayerName}");
            PsuPlugin.PenumbraService.RedrawObject.Invoke(player.ObjectIndex);
            Svc.Log.Debug($"Redrawed player {player.Name.TextValue}");
        }
    }
}