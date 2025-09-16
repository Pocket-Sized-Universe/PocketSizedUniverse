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

        Svc.Log.Debug("Updating local player data.");

        var penumbra = new PenumbraData();
        var manips = PsuPlugin.PenumbraService.GetPlayerMetaManipulations.Invoke();

        // Resolve current character resource paths (local file -> game paths)
        var resourcePaths = Svc.Framework.RunOnFrameworkThread(() =>
            PsuPlugin.PenumbraService.GetGameObjectResourcePaths.Invoke(localPlayer.ObjectIndex)).Result;
        if (resourcePaths.Length == 0)
        {
            Svc.Log.Warning("Failed to get character resource paths from Penumbra.");
            return;
        }

        var resolvedPaths = resourcePaths[0];

        string? packPath = PsuPlugin.Configuration.MyStarPack?.GetDataPack()?.FilesPath;
        if (packPath == null)
        {
            Svc.Log.Warning("My star pack data pack path is null.");
            return;
        }

        // Build file list directly from resolved paths, filtering like Mare
        var files = new List<CustomRedirect>();
        foreach (var pathMapping in resolvedPaths)
        {
            var localPath = pathMapping.Key;
            var gamePaths = pathMapping.Value.ToList();
            if (!File.Exists(localPath))
                continue; // swaps are handled below

            var ext = Path.GetExtension(localPath);
            var allowedGamePaths = gamePaths.Where(gp => AllowedFileExtensions.IsAllowed(gp, ext)).ToList();
            if (allowedGamePaths.Count == 0)
                continue;

            try
            {
                var data = await File.ReadAllBytesAsync(localPath);
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(data);
                var redirectedFile = new CustomRedirect(hash)
                {
                    // Prefer original file extension; fallback to first game path's extension if missing
                    FileExtension = string.IsNullOrWhiteSpace(Path.GetExtension(localPath))
                        ? Path.GetExtension(allowedGamePaths.FirstOrDefault() ?? string.Empty)
                        : Path.GetExtension(localPath)
                };

                redirectedFile.ApplicableGamePaths = allowedGamePaths;

                var redirectPath = redirectedFile.GetPath(packPath);
                if (!File.Exists(redirectPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(redirectPath)!);
                    await File.WriteAllBytesAsync(redirectPath, data);
                }

                files.Add(redirectedFile);
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Error processing file {localPath}: {ex.Message}");
            }
        }

        // Build file swaps from resource tree where the actual path is not a local file
        var fileSwaps = new List<AssetSwap>();
        var penumbraResourceTree = PsuPlugin.PenumbraService.GetPlayerResourceTrees.Invoke();
        foreach (var resourceTree in penumbraResourceTree)
        {
            foreach (var directSwap in resourceTree.Value.Nodes.Where(n => !File.Exists(n.ActualPath)))
            {
                if (directSwap.GamePath == null) continue;
                var ext = Path.GetExtension(directSwap.ActualPath);
                if (!AllowedFileExtensions.IsAllowed(directSwap.GamePath, ext)) continue;

                var swap = new AssetSwap(
                    NormalizePenumbraPath(directSwap.GamePath),
                    NormalizePenumbraPath(directSwap.ActualPath)!);
                fileSwaps.Add(swap);
            }
        }

        penumbra.Files = files;
        penumbra.FileSwaps = fileSwaps;
        penumbra.MetaManipulations = manips;

        // Glamourer
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

    private async Task<List<CustomRedirect>> ProcessModFilesAsync(string modDir, string packPath,
        Dictionary<string, List<string>> fileToGamePaths)
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
            if (!File.Exists(playerDataLoc) || await File.ReadAllTextAsync(playerDataLoc) != encodedData)
            {
                await File.WriteAllTextAsync(playerDataLoc, encodedData);
                Svc.Log.Debug("Updated basic info on disk.");
            }

            // Save Penumbra data
            var penumbraLoc = PenumbraData.GetPath(myDataPack.DataPath);
            var encodedPenumbra = Base64Util.ToBase64(PenumbraData);
            if (!File.Exists(penumbraLoc) || await File.ReadAllTextAsync(penumbraLoc) != encodedPenumbra)
            {
                await File.WriteAllTextAsync(penumbraLoc, encodedPenumbra);
                Svc.Log.Debug("Updated penumbra data on disk.");
            }

            // Save Glamourer data
            var glamourerLoc = GlamourerData.GetPath(myDataPack.DataPath);
            var encodedGlamourer = Base64Util.ToBase64(GlamourerData);
            if (!File.Exists(glamourerLoc) || await File.ReadAllTextAsync(glamourerLoc) != encodedGlamourer)
            {
                await File.WriteAllTextAsync(glamourerLoc, encodedGlamourer);
                Svc.Log.Debug("Updated glamourer data on disk.");
            }
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