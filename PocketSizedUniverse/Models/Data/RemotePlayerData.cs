using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;

namespace PocketSizedUniverse.Models.Data;

public class RemotePlayerData(StarPack starPack) : PlayerData(starPack)
{
    private uint LockKey { get; } = (uint)Random.Shared.Next();
    public sealed override IPlayerCharacter? Player { get; set; }

    public Guid? AssignedCollectionId { get; set; }

    public Guid? AssignedCustomizeProfileId { get; set; }

    public bool ApplyBasicIfChanged(BasicData newBasic)
    {
        var changed = Data == null
            || !string.Equals(Data.PlayerName, newBasic.PlayerName, StringComparison.Ordinal)
            || Data.WorldId != newBasic.WorldId;

        if (changed)
        {
            Data = newBasic;
            Svc.Log.Debug($"[Remote] Basic changed for {StarPackReference.StarId}: {Data.PlayerName}@{Data.WorldId}");
        }

        return changed;
    }

    public bool ApplyGlamourerIfChanged(GlamourerData newGlam)
    {
        var changed = GlamourerData == null
            || !string.Equals(GlamourerData.GlamState, newGlam.GlamState, StringComparison.Ordinal);

        if (!changed)
            return false;

        if (Player == null)
        {
            // Keep the new state but do not attempt to apply when player is not available
            GlamourerData = newGlam;
            Svc.Log.Debug("[Remote] Glamourer changed but player not nearby; will apply when available.");
            return false;
        }

        GlamourerData = newGlam;
        PsuPlugin.GlamourerService.ApplyState.Invoke(GlamourerData.GlamState, Player.ObjectIndex, LockKey);
        Svc.Log.Debug("[Remote] Applied Glamourer state.");
        return true;
    }

    public bool ApplyHonorificIfChanged(HonorificData newHonorific)
    {
        var changed = HonorificData == null || !string.Equals(HonorificData.Title, newHonorific.Title, StringComparison.Ordinal);

        if (!changed)
            return false;

        if (Player == null)
        {
            // Keep the new state but do not attempt to apply when player is not available
            HonorificData = newHonorific;
            Svc.Log.Debug("[Remote] Honorific changed but player not nearby; will apply when available.");
            return false;
        }
        HonorificData = newHonorific;
        var existingTitle = PsuPlugin.HonorificService.GetCharacterTitle(Player.ObjectIndex);
        if (!string.IsNullOrEmpty(HonorificData.Title) && !string.Equals(existingTitle, HonorificData.Title, StringComparison.Ordinal))
        {
            PsuPlugin.HonorificService.SetCharacterTitle(Player.ObjectIndex, HonorificData.Title);
            Svc.Log.Debug("[Remote] Applied Honorific title.");
        }
        return false;
    }

    public bool ApplyCustomzieIfChanged(CustomizeData newCustomize)
    {
        var changed = CustomizeData == null
            || !string.Equals(CustomizeData.CustomizeState, newCustomize.CustomizeState, StringComparison.Ordinal);

        if (!changed)
            return false;

        if (Player == null)
        {
            // Keep the new state but do not attempt to apply when player is not available
            CustomizeData = newCustomize;
            Svc.Log.Debug("[Remote] Customize changed but player not nearby; will apply when available.");
            return false;
        }
        CustomizeData = newCustomize;
        if (AssignedCustomizeProfileId == null && !string.IsNullOrEmpty(CustomizeData.CustomizeState))
        {
            var apply = PsuPlugin.CustomizeService.ApplyTemporaryCustomizeProfileOnCharacter(Player.ObjectIndex, CustomizeData.CustomizeState);
            if (apply.Item1 > 0)
            {
                Svc.Log.Warning($"Failed to apply temporary customize profile. Error {apply.Item1}");
                return false;
            }
            Svc.Log.Debug("[Remote] Applied Customize state.");
            AssignedCustomizeProfileId = apply.Item2;
            return true;
        }
        if (string.IsNullOrEmpty(CustomizeData.CustomizeState))
        {
            var remove = PsuPlugin.CustomizeService.DeleteTemporaryCustomizeProfileOnCharacter(Player.ObjectIndex);
            if (remove > 0)
            {
                Svc.Log.Warning($"Failed to delete temporary customize profile. Error {remove}");
                return false;
            }

            return true;
        }

        return false;
    }

    public bool ApplyPenumbraIfChanged(PenumbraData newPenumbra)
    {
        var changed = PenumbraData == null
            || !string.Equals(PenumbraData.MetaManipulations, newPenumbra.MetaManipulations, StringComparison.Ordinal)
            || !UnorderedEqualByKey(PenumbraData.Files, newPenumbra.Files, FileKey)
            || !UnorderedEqualByKey(PenumbraData.FileSwaps, newPenumbra.FileSwaps, SwapKey);

        if (!changed)
            return false;

        if (Player == null)
        {
            // Stash change; apply later when player exists
            PenumbraData = newPenumbra;
            Svc.Log.Debug("[Remote] Penumbra changed but player not nearby; will apply when available.");
            return false;
        }

        // Ensure collection
        if (AssignedCollectionId == null)
        {
            PsuPlugin.PenumbraService.CreateTemporaryCollection.Invoke(
                "PocketSizedUniverse", "PSU_" + newPenumbra.Id, out var collectionId);
            AssignedCollectionId = collectionId;
        }

        // Meta manipulations
        var metaModName = $"PSU_Meta_{newPenumbra.Id}";
        Svc.Log.Debug($"[Remote] Removing existing meta mod {metaModName} from collection {AssignedCollectionId}");
        PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(metaModName, AssignedCollectionId.Value, 0);
        if (!string.IsNullOrEmpty(newPenumbra.MetaManipulations))
        {
            Svc.Log.Debug($"[Remote] Adding meta mod {metaModName} to collection {AssignedCollectionId.Value}");
            PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(
                metaModName, AssignedCollectionId.Value, new Dictionary<string, string>(),
                newPenumbra.MetaManipulations, 0);
        }

        // File redirects and swaps
        var paths = new Dictionary<string, string>();
        foreach (var customFile in newPenumbra.Files)
        {
            var localFilePath = customFile.GetPath(StarPackReference.GetDataPack()!.FilesPath);
            if (!File.Exists(localFilePath))
            {
                Svc.Log.Debug($"[Remote] Custom file missing: {localFilePath}");
                continue;
            }

            foreach (var gamePath in customFile.ApplicableGamePaths)
            {
                if (string.IsNullOrWhiteSpace(gamePath)) continue;
                paths[gamePath] = localFilePath;
            }
        }

        foreach (var assetSwap in newPenumbra.FileSwaps)
        {
            if (assetSwap.GamePath != null)
                paths[assetSwap.GamePath] = assetSwap.RealPath;
        }

        var fileModName = $"PSU_File_{newPenumbra.Id}";
        Svc.Log.Debug($"[Remote] Removing existing file mod {fileModName} from collection {AssignedCollectionId.Value}");
        PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(fileModName, AssignedCollectionId.Value, 0);
        if (paths.Count > 0)
        {
            PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(fileModName, AssignedCollectionId.Value, paths, string.Empty, 0);
            Svc.Log.Debug($"[Remote] Added file mod {fileModName} with {paths.Count} mappings.");
        }

        PenumbraData = newPenumbra;
        PsuPlugin.PenumbraService.AssignTemporaryCollection.Invoke(AssignedCollectionId.Value, Player.ObjectIndex);
        PsuPlugin.PenumbraService.RedrawObject.Invoke(Player.ObjectIndex);
        return true;
    }
}
