using System.Collections.Concurrent;
using System.ComponentModel;
using AntiVirus;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using ECommons;
using Ipfs;
using Microsoft.Extensions.Logging;
using Penumbra.Api.IpcSubscribers;
using PocketSizedUniverse.Data;
using PocketSizedUniverse.Services;
using PocketSizedUniverse.Util;

namespace PocketSizedUniverse.Controllers;

public class ModController : IDisposable
{
    private readonly ILogger<ModController> _logger;
    private readonly IFramework _framework;
    private readonly Config.Configuration _configuration;
    private readonly PenumbraService _penumbraService;
    private readonly IpfsService _ipfsService;
    private readonly PlayerDataService _playerDataService;
    private readonly IObjectTable _objectTable;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly CancellationTokenSource _cts = new();
    private readonly AntiVirusService _antiVirusService;
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    private ConcurrentDictionary<string, (Cid cid, DateTime lastModified)> FileHashCache { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    
    public ConcurrentDictionary<Cid, string> CidToFilePathCache { get; } = new();

    public ModController(IFramework framework, Config.Configuration configuration, PenumbraService penumbraService,
        ILogger<ModController> logger, IpfsService ipfsService, IObjectTable objectTable,
        IDalamudPluginInterface pluginInterface, AntiVirusService antiVirusService,
        PlayerDataService playerDataService)
    {
        _pluginInterface = pluginInterface;
        _logger = logger;
        _framework = framework;
        _configuration = configuration;
        _penumbraService = penumbraService;
        _playerDataService = playerDataService;
        _ipfsService = ipfsService;
        _objectTable = objectTable;
        _antiVirusService = antiVirusService;
        _logger.LogInformation("Mod Controller created");
        GameObjectResourcePathResolved.Subscriber(pluginInterface, OnObjectPathResolved).Enable();

        Task.Run(() => DoModWork(_cts.Token), _cts.Token);
    }

    private async Task DoModWork(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_playerDataService.LocalPlayerData == null || !_ipfsService.DaemonIsReady) continue;
                await UpdatePenumbraData(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background mod work loop.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task UpdatePenumbraData(CancellationToken token = default)
    {
        if (!await _updateLock.WaitAsync(0, token))
        {
            _logger.LogDebug("UpdatePenumbraData is already running, skipping.");
            return;
        }

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = 3
        };

        try
        {
            if (_playerDataService.LocalPlayerData == null) return;
            var manips = _penumbraService.GetPlayerMetaManipulations.Invoke();
            _playerDataService.LocalPlayerData.MetaManipulations = manips;

            var playerResources = _penumbraService.GetPlayerResourcePaths.Invoke();
            var resourcePathsArr = playerResources.Values.ToArray();
            if (resourcePathsArr.Length == 0)
            {
                _logger.LogWarning("Failed to get character resource paths from Penumbra.");
                return;
            }

            var modFiles = new ConcurrentBag<CustomAsset>();
            var assetSwaps = new ConcurrentBag<AssetSwap>();

            // Join the array of dictionaries into a single dictionary
            // where the keys are the real paths and the values are the game paths.
            var resolvedPaths = resourcePathsArr
                .SelectMany(dict => dict)
                .GroupBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(kvp => kvp.Value).Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (var (realPath, gamePaths) in _configuration.TransientFilesData)
            {
                if (string.IsNullOrWhiteSpace(realPath) || gamePaths.Count == 0)
                    continue;
                if (resolvedPaths.TryGetValue(realPath, out var existing))
                {
                    existing.UnionWith(gamePaths);
                }
                else
                {
                    resolvedPaths[realPath] = gamePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
            }

            if (_configuration.Dirty)
            {
                _configuration.Save();
                _configuration.Dirty = false;
            }

            var resourcePaths = resolvedPaths
                .Where(kvp => File.Exists(kvp.Key))
                .GroupBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    IReadOnlyList<string> (g) => g.SelectMany(kvp => kvp.Value)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase
                );

            var swaps = resolvedPaths
                .Where(kvp => !File.Exists(kvp.Key))
                .SelectMany(kvp => kvp.Value.Where(v => !string.Equals(v, kvp.Key))
                    .Select(gp => (GamePath: gp, RealPath: kvp.Key)))
                .OrderBy(s => s.GamePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.RealPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var (gamePath, realPath) in swaps)
            {
                assetSwaps.Add(new AssetSwap(gamePath, realPath));
            }

            await Parallel.ForEachAsync(resourcePaths.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase),
                parallelOptions, async (mod, ct) =>
                {
                    try
                    {
                        Cid? cid = null;
                        var fileInfo = new FileInfo(mod.Key);
                        var lastModified = fileInfo.LastWriteTimeUtc;

                        if (FileHashCache.TryGetValue(mod.Key, out var cached) && cached.lastModified == lastModified)
                        {
                            cid = cached.cid;
                        }
                        else
                        {

                            cid = await _ipfsService.AddAndPinFile(mod.Key, ct);
                            if (cid == null)
                            {
                                _logger.LogError("Failed to add file {FilePath} to IPFS", mod.Key);
                                return;
                            }

                            FileHashCache[mod.Key] = (cid, lastModified);
                        }

                        modFiles.Add(new CustomAsset()
                        {
                            Extension = fileInfo.Extension,
                            Cid = cid,
                            ApplicablePaths = mod.Value.ToList()
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Pinning timed out for file: {FilePath}", mod.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file {FilePath}", mod.Key);
                    }
                });

            foreach (var custom in modFiles)
            {
                if (_playerDataService.LocalPlayerData.ModFiles.Contains(custom)) continue;
                _playerDataService.LocalPlayerData.ModFiles.Add(custom);
                _playerDataService.LocalDataDirty = true;
            }
            foreach (var swap in assetSwaps)
            {
                if (_playerDataService.LocalPlayerData.AssetSwaps.Contains(swap)) continue;
                _playerDataService.LocalPlayerData.AssetSwaps.Add(swap);
                _playerDataService.LocalDataDirty = true;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Penumbra service canceled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Penumbra data");
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public Guid? ApplyData(int objectIndex, string metaManips, Dictionary<string, string> files, Guid? collectionId,
        Guid userCid)
    {
        if (collectionId != null)
        {
            CleanupData(collectionId.Value, userCid, objectIndex);
        }

        _penumbraService.CreateTemporaryCollection.Invoke(
            "PocketSizedUniverse", GetCollectionName(userCid), out var newColl);
        collectionId = newColl;

        _penumbraService.AddTemporaryMod.Invoke(GetMetaName(userCid), collectionId.Value,
            new Dictionary<string, string>(), metaManips, 0);
        _penumbraService.AddTemporaryMod.Invoke(GetFilesName(userCid), collectionId.Value, files, string.Empty, 0);
        _penumbraService.AssignTemporaryCollection.Invoke(collectionId.Value, objectIndex);
        _penumbraService.RedrawObject.Invoke(objectIndex);

        return collectionId;
    }

    private string GetCollectionName(Guid cid) => $"PSU_{cid}";
    private string GetMetaName(Guid cid) => $"PSU_Meta_{cid}";
    private string GetFilesName(Guid cid) => $"PSU_Files_{cid}";

    public void CleanupData(Guid collectionId, Guid userCid, int? objectIndex)
    {
        _penumbraService.RemoveTemporaryMod.Invoke(GetMetaName(userCid), collectionId, 0);
        _penumbraService.RemoveTemporaryMod.Invoke(GetFilesName(userCid), collectionId, 0);
        _penumbraService.DeleteTemporaryCollection.Invoke(collectionId);
        if (objectIndex != null)
            _penumbraService.RedrawObject.Invoke(objectIndex.Value);
    }

    private void OnObjectPathResolved(nint gameObject, string gamePath, string localPath)
    {
        var capturedGamePath = gamePath;
        var capturedLocalPath = localPath;
        _ = Task.Run(async () =>
        {
            try
            {
                await _framework.RunOnFrameworkThread(() =>
                {
                    var realLocalPath = capturedLocalPath.Split('|').Last();
                    var realObj = _objectTable.CreateObjectReference(gameObject);
                    var player = _objectTable.LocalPlayer;
                    if (realObj == null || player == null)
                        return;
                    if (realObj.ObjectIndex - 1 == player.ObjectIndex ||
                        realObj.ObjectIndex == player.ObjectIndex ||
                        realObj.OwnerId == player.EntityId)
                    {
                        _ = Task.Run(() =>
                        {
                            var ext = Path.GetExtension(realLocalPath);
                            // ReSharper disable once PossibleUnintendedLinearSearchInSet
                            if (AllowedFileExtensions.AlwaysExclude.Contains(ext, StringComparer.OrdinalIgnoreCase) ||
                                AllowedFileExtensions.Normal.Contains(ext)) return;
                            var normalizedGamePath = NormalizePenumbraPath(capturedGamePath);
                            var normalizedRealPath = NormalizePenumbraPath(realLocalPath);
                            if (normalizedGamePath == null || normalizedRealPath == null)
                                return;
                            if (string.Equals(normalizedRealPath, normalizedGamePath))
                                return;
                            if (_configuration.TransientFilesData.TryGetValue(normalizedRealPath,
                                    out var transientData))
                            {
                                if (!transientData.Contains(normalizedGamePath))
                                    transientData.Add(normalizedGamePath);
                                _configuration.TransientFilesData[normalizedRealPath] = transientData;
                                _configuration.Dirty = true;
                            }
                            else
                            {
                                var hashSet = new List<string>() { normalizedGamePath };
                                _configuration.TransientFilesData[normalizedRealPath] = hashSet;
                                _configuration.Dirty = true;
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in OnObjectPathResolved: {Exception}", ex);
            }
        });
    }

    public ConcurrentDictionary<Cid, Task> FileResolveTasks { get; } = new();
    public async Task PreparePaths(RemoteData remoteData)
    {
        var assets = remoteData.PlayerData?.ModFiles;
        var swaps = remoteData.PlayerData?.AssetSwaps;
        if (assets == null || swaps == null)
            return;
        ConcurrentDictionary<string, string> paths = new();
        foreach (var f in assets)
        {
            if (CidToFilePathCache.TryGetValue(f.Cid, out var cachedPath) && File.Exists(cachedPath))
            {
                foreach (var gamePath in f.ApplicablePaths)
                {
                    paths[gamePath] = cachedPath;
                }
            }
            else if (!FileResolveTasks.TryGetValue(f.Cid, out var task))
            {
                FileResolveTasks[f.Cid] = Task.Run(async () =>
                {
                    try
                    {
                        var filePath = await _ipfsService.ResolveCidToFilePath(f);
                        if (filePath != null)
                        {
                            if (!Dalamud.Utility.Util.IsWine())
                            {
                                var avScan = _antiVirusService.ScanFile(filePath);
                                if (avScan != ScanResult.VirusNotFound)
                                {
                                    _logger.LogWarning("File {FilePath} scanned with result {Result}", filePath, avScan);
                                    return;
                                }
                            }
                            CidToFilePathCache[f.Cid] = filePath;
                            foreach (var gamePath in f.ApplicablePaths)
                            {
                                paths[gamePath] = filePath;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error resolving file for CID {Cid}", f.Cid);
                    }
                });
            }
        }
        await Task.WhenAll(FileResolveTasks.Values);

        foreach (var s in swaps)
        {
            if (string.IsNullOrWhiteSpace(s.From) || string.IsNullOrWhiteSpace(s.To)) continue;
            paths[s.From] = s.To;
        }

        remoteData.PreparedPaths = paths.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        PathsReady?.Invoke(this, new PathsReadyEventArgs(remoteData.PairId));
    }
    
    public event EventHandler<PathsReadyEventArgs>? PathsReady;
    public class PathsReadyEventArgs(Guid pairId) : EventArgs
    {
        public Guid PairId { get; } = pairId;
    }

    private static string? NormalizePenumbraPath(string? path)
    {
        return path == null ? null : string.Intern(path.Replace('\\', '/'));
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        GameObjectResourcePathResolved.Subscriber(_pluginInterface, OnObjectPathResolved).Disable();
        GC.SuppressFinalize(this);
    }
}