using System.Security.Cryptography;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json;
using Penumbra.Api.Enums;
using PocketSizedUniverse.Models.Mods;
using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models.Data;

public class PlayerData
{
    /// <summary>
    /// Normalizes Penumbra paths to use forward slashes for cross-platform consistency.
    /// Penumbra GamePaths use forward slashes while ActualPaths may use backslashes on Windows.
    /// </summary>
    /// <param name="path">The path to normalize</param>
    /// <returns>A normalized path using forward slashes, or null if input was null</returns>
    private static string? NormalizePenumbraPath(string? path)
    {
        if (path == null) return null;
        return string.Intern(path.Replace('\\', '/'));
    }
    public async Task PopulateFromLocalAsync(IPlayerCharacter player)
    {
        var localPlayer = Svc.Framework.RunOnFrameworkThread(() => Player.Object).Result;
        if (localPlayer == null)
        {
            Svc.Log.Warning("Local player object is null.");
            return;
        }

        var penumbra = new PenumbraData();
        Svc.Log.Debug("Updating local player data.");
        var manips = PsuPlugin.PenumbraService.GetPlayerMetaManipulations.Invoke();
        var mods = new List<SyncedMod>();

        var resourcePaths = Svc.Framework.RunOnFrameworkThread(() => PsuPlugin.PenumbraService.GetGameObjectResourcePaths.Invoke(localPlayer.ObjectIndex)).Result;
        if (resourcePaths.Length == 0)
        {
            Svc.Log.Warning("Failed to get character resource paths from Penumbra.");
            return;
        }

        var resolvedPaths = resourcePaths[0];
        Dictionary<string, List<string>> fileToGamePaths = new();
        
        foreach (var pathMapping in resolvedPaths)
        {
            var localPath = pathMapping.Key;
            var gamePaths = pathMapping.Value.ToList();
            
            if (!fileToGamePaths.TryAdd(localPath, gamePaths))
            {
                fileToGamePaths[localPath].AddRange(gamePaths);
            }
        }
        
        Svc.Log.Debug($"Found {fileToGamePaths.Count} file mappings from Penumbra");
        
        var penumbraResourceTree = PsuPlugin.PenumbraService.GetPlayerResourceTrees.Invoke();
        foreach (var resourceTree in penumbraResourceTree)
        {
            var effectiveCollectionId = PsuPlugin.PenumbraService.GetCollectionForObject.Invoke(resourceTree.Key);
            if (!effectiveCollectionId.ObjectValid)
            {
                Svc.Log.Warning("Local player object not valid");
                return;
            }

            var settings =
                PsuPlugin.PenumbraService.GetAllModSettings.Invoke(effectiveCollectionId.EffectiveCollection.Id);
            if (settings.Item1 != PenumbraApiEc.Success || settings.Item2 == null)
            {
                Svc.Log.Warning("Failed to get mod settings.");
                return;
            }

            var penumbraDir = PsuPlugin.PenumbraService.GetModDirectory.Invoke();
            string? packPath = PsuPlugin.Configuration.MyStarPack?.GetDataPack()?.FilesPath;
            if (packPath == null)
            {
                Svc.Log.Warning("My star pack data pack path is null.");
                return;
            }

            foreach (var activeMod in settings.Item2)
            {
                if (!activeMod.Value.Item1)
                {
                    //Svc.Log.Debug($"Skipping disabled mod {activeMod.Key}");
                    continue;
                }
                var modDir = Path.Combine(penumbraDir, activeMod.Key);
                var mod = activeMod.Value;
                if (!Directory.Exists(modDir))
                {
                    Svc.Log.Warning($"Mod directory {modDir} does not exist.");
                    continue;
                }

                var syncMod = new SyncedMod()
                {
                    Enabled = mod.Item1,
                    Priority = mod.Item2,
                    Settings = mod.Item3,
                    Inherited = mod.Item4,
                    Temporary = mod.Item5,
                };

                // Process files in the background but wait for completion before continuing
                var customFiles = await ProcessModFilesAsync(modDir, packPath, fileToGamePaths);
                syncMod.CustomFiles = customFiles;
                foreach (var directSwap in resourceTree.Value.Nodes.Where(n => !File.Exists(n.ActualPath)))
                {
                    if (directSwap.GamePath == null || directSwap.ActualPath.EndsWith("imc") || directSwap.GamePath.EndsWith("imc")) continue;
                    var swap = new AssetSwap(
                        NormalizePenumbraPath(directSwap.GamePath), 
                        NormalizePenumbraPath(directSwap.ActualPath)!);
                    syncMod.AssetSwaps.Add(swap);
                }
                mods.Add(syncMod);
            }
        }

        penumbra.Mods = mods;
        penumbra.MetaManipulations = manips;

        var glamState = PsuPlugin.GlamourerService.GetStateBase64.Invoke(localPlayer.ObjectIndex);
        if (glamState.Item2 == null)
        {
            Svc.Log.Warning("Failed to get glamourer state.");
            return;
        }

        var glamData = new GlamourerData()
        {
            GlamState = glamState.Item2
        };

        Data = new BasicData()
        {
            PlayerName = localPlayer.Name.TextValue,
            WorldId = localPlayer.HomeWorld.RowId,
        };
        PenumbraData = penumbra;
        GlamourerData = glamData;
    }

    private async Task<List<CustomRedirect>> ProcessModFilesAsync(string modDir, string packPath, Dictionary<string, List<string>> fileToGamePaths)
    {
        var customFiles = new List<CustomRedirect>();

        try
        {
            // Get files to process
            var filesToProcess = Directory.GetFiles(modDir, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".json"))
                .ToList();

            foreach (var file in filesToProcess)
            {
                try
                {
                    if (!fileToGamePaths.TryGetValue(file, out var gamePaths) || gamePaths.Count == 0)
                    {
                        //Svc.Log.Debug("No gamepaths found for mod.");
                        continue;
                    }
                    Svc.Log.Debug($"Found {gamePaths.Count} game paths for file {file}");
                    //Svc.Log.Debug($"Processing file: {file}");
                    var data = await File.ReadAllBytesAsync(file);
                    using var sha256 = SHA256.Create();
                    var hash = sha256.ComputeHash(data);
                    var redirectedFile = new CustomRedirect(hash)
                    {
                        // Prefer original file extension to ensure proper loader selection on the receiver
                        FileExtension = Path.GetExtension(file)
                    };
                    if (string.IsNullOrWhiteSpace(redirectedFile.FileExtension))
                    {
                        // Fallback to the first game path's extension if the source file had none
                        redirectedFile.FileExtension = Path.GetExtension(gamePaths.FirstOrDefault() ?? string.Empty);
                    }

                    redirectedFile.ApplicableGamePaths = gamePaths;

                    var redirectPath = redirectedFile.GetPath(packPath);
                    if (File.Exists(redirectPath))
                    {
                        //Svc.Log.Debug($"Found redirected file: {redirectPath}");
                    }
                    else
                    {
                        //Svc.Log.Debug($"Moving new file {file} to {redirectPath}");
                        // Ensure directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(redirectPath)!);
                        await File.WriteAllBytesAsync(redirectPath, data);
                    }

                    customFiles.Add(redirectedFile);
                }
                catch (Exception ex)
                {
                    Svc.Log.Error($"Error processing file {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error processing mod directory {modDir}: {ex.Message}");
        }

        return customFiles;
    }

    public async Task SavePlayerDataToDiskAsync()
    {
        try
        {
            Svc.Log.Debug("Updating local player data on disk.");

            var myDataPack = PsuPlugin.Configuration.MyStarPack?.GetDataPack();
            if (myDataPack == null)
            {
                Svc.Log.Warning("My star pack data pack is null.");
                return;
            }

            // Save basic data
            var playerDataLoc = Data.GetPath(myDataPack.DataPath);
            var encodedData = Base64Util.ToBase64(Data);
            await File.WriteAllTextAsync(playerDataLoc, encodedData);
            Svc.Log.Debug("Updated basic info on disk.");

            // Save Penumbra data
            var penumbraLoc = PenumbraData.GetPath(myDataPack.DataPath);
            var encodedPenumbra = Base64Util.ToBase64(PenumbraData);
            await File.WriteAllTextAsync(penumbraLoc, encodedPenumbra);
            Svc.Log.Debug("Updated penumbra data on disk.");

            // Save Glamourer data
            var glamourerLoc = GlamourerData.GetPath(myDataPack.DataPath);
            var encodedGlamourer = Base64Util.ToBase64(GlamourerData);
            await File.WriteAllTextAsync(glamourerLoc, encodedGlamourer);
            Svc.Log.Debug("Updated glamourer data on disk.");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error saving player data to disk: {ex}");
        }
    }

    public Task PopulateFromDiskAsync()
    {
        try
        {
            var dataPack = StarPackReference.GetDataPack();
            if (dataPack == null)
            {
                Svc.Log.Warning($"Failed to load data pack {StarPackReference.DataPackId}");
            }

            var data = BasicData.LoadFromDisk(dataPack.DataPath);
            if (data == null)
            {
                Svc.Log.Warning($"Failed to load data from disk for {StarPackReference.StarId}");
            }

            var penumbraData = PenumbraData.LoadFromDisk(dataPack.DataPath);
            if (penumbraData == null)
            {
                Svc.Log.Warning($"Failed to load penumbra data from disk for {StarPackReference.StarId}");
            }

            var glamData = GlamourerData.LoadFromDisk(dataPack.DataPath);
            if (glamData == null)
            {
                Svc.Log.Warning($"Failed to load glamourer data from disk for {StarPackReference.StarId}");
            }
            if (data == null || penumbraData == null || glamData == null) return Task.CompletedTask;

            Data = data;
            PenumbraData = penumbraData;
            GlamourerData = glamData;
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error loading player data from disk: {ex}");
            throw;
        }
    }

    public BasicData Data { get; private set; }

    public PenumbraData PenumbraData { get; private set; }

    public GlamourerData GlamourerData { get; private set; }

    public StarPack StarPackReference { get; private set; }

    public PlayerData(StarPack starPack)
    {
        StarPackReference = starPack;
    }
}