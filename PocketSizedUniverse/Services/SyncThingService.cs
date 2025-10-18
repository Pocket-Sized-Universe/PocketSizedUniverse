using System.Collections.Concurrent;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Models.Syncthing.Models.Response;
using PocketSizedUniverse.Models.Syncthing.Models.Response.EventData;
using Syncthing;
using Syncthing.Http;
using Syncthing.Models.Response;
using Connection = Syncthing.Http.Connection;

namespace PocketSizedUniverse.Services;

public class SyncThingService : ICache, IDisposable
{
    public ConcurrentDictionary<string, Star> Stars { get; } = new();
    public ConcurrentDictionary<Guid, DataPack> DataPacks { get; } = new();

    public ConnectionsResponse? Connections { get; set; }

    private SyncthingClient? _client;
    public DateTime LastRefresh { get; private set; } = DateTime.MinValue;

    public bool IsHealthy { get; private set; } = false;

    private int LastSeenEvent { get; set; } = 0;

    // Track connection byte totals to compute transfer rates between refreshes
    private class ByteSnapshot
    {
        public long InBytesTotal { get; init; }
        public long OutBytesTotal { get; init; }
        public DateTime At { get; init; }
        public bool Connected { get; init; }
    }

    public sealed class TransferRates
    {
        public double InBps { get; init; } // bytes per second
        public double OutBps { get; init; }
        public long InBytesTotal { get; init; }
        public long OutBytesTotal { get; init; }
        public DateTime At { get; init; }
        public bool Connected { get; init; }
        public bool IsLocal { get; init; }
    }

    private readonly ConcurrentDictionary<string, ByteSnapshot> _prevSnapshots = new();
    private readonly ConcurrentDictionary<string, TransferRates> _currentRates = new();

    public SyncThingService()
    {
        Svc.Framework.Update += LocalUpdate;
        InitializeClient();
    }

    public void InitializeClient()
    {
        if (string.IsNullOrWhiteSpace(PsuPlugin.Configuration.ApiKey) || PsuPlugin.Configuration.ApiUri == null)
        {
            _client = null;
            return;
        }

        try
        {
            var credentials = new Credentials(PsuPlugin.Configuration.ApiKey);
            var store = new InMemoryCredentialStore(credentials);
            var connection = new Connection(PsuPlugin.Configuration.ApiUri, store);
            _client = new SyncthingClient(connection);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to initialize SyncThingService: {ex}");
            _client = null;
        }
    }

    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(60);
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    public DateTime LastGalaxyUpdate { get; set; } = DateTime.MinValue;

    public void LocalUpdate(IFramework framework)
    {
        if (_client == null)
            InitializeClient();
        // Periodically refresh caches and stats
        if (DateTime.UtcNow - LastUpdated >= UpdateInterval)
        {
            LastUpdated = DateTime.UtcNow;
            RefreshCaches();
            ProcessEvents();
            RemoveUnpairedStarsAndDataPacks();
            if (!PsuPlugin.IsRunningUnderWine() && TimeSpan.FromSeconds(PsuPlugin.Configuration.GalaxyPollingSeconds) < DateTime.UtcNow - LastGalaxyUpdate)
            {
                LastGalaxyUpdate = DateTime.UtcNow;
                foreach (var galaxy in PsuPlugin.Configuration.Galaxies)
                {
                    _ = Task.Run(galaxy.TryFetch);
                }
            }
        }

        // Invalidate caches if it's been a while to ensure we reconcile with config changes
        if (TimeSpan.FromMinutes(1) < DateTime.UtcNow - LastRefresh)
        {
            InvalidateCaches();
        }
    }

    private void ProcessEvents()
    {
        _ = Task.Run(async () =>
        {
            if (_client == null || !IsHealthy)
                return;

            try
            {
                var events = await _client.Config.Events.Get(LastSeenEvent).ConfigureAwait(false);
                foreach (var ev in events)
                {
                    LastSeenEvent = Math.Max(LastSeenEvent, ev.Id);
                    if (ev.Type == EventType.FolderCompletion)
                    {
                        var data = JsonConvert.DeserializeObject<FolderCompletion>(ev.Data.ToString()!);
                        if (data == null)
                        {
                            Svc.Log.Warning("FolderCompletion event has no data");
                            continue;
                        }
                        if (data.Folder == PsuPlugin.Configuration.MyStarPack?.DataPackId.ToString())
                            continue;
                        if (!PsuPlugin.PlayerDataService.RemotePlayerData.TryGetValue(data.Device, out var remoteData))
                            continue;
                        if (data.Completion < 100 || data.RemoteState != "valid")
                        {
                            Svc.Log.Debug($"FolderCompletion event for folder {data.Folder} from star {data.Device} - not complete");
                            continue;
                        }
                        if (ev.Time.ToUniversalTime() > remoteData.LastUpdated)
                        {
                            Svc.Log.Debug($"FolderCompletion event for folder {data.Folder} from star {data.Device} - application required");
                            if (!PsuPlugin.PlayerDataService.PendingCleanups.Contains(data.Device))
                                PsuPlugin.PlayerDataService.PendingCleanups.Enqueue(data.Device);
                            if (!PsuPlugin.PlayerDataService.PendingReads.Contains(data.Device))
                                PsuPlugin.PlayerDataService.PendingReads.Enqueue(data.Device);
                        }
                        else
                        {
                            Svc.Log.Debug($"FolderCompletion event for folder {data.Folder} from star {data.Device} - no action required");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Failed to process SyncThing events: {ex}");
            }
        });
    }

    public void InvalidateCaches()
    {
        DataPacks.Clear();
        Stars.Clear();
        RefreshCaches();
    }

    private async Task<Star> EnsureStarInCache(string starId)
    {
        if (!Stars.TryGetValue(starId, out var existingStar))
        {
            Svc.Log.Debug($"[DEBUG] Creating new Star: {starId}");
            var star = new Star()
            {
                StarId = starId,
                AutoAcceptFolders = false,
            };

            Svc.Log.Debug(
                $"[DEBUG] Star object created - StarId: {star.StarId}, Name: {star.Name}, AutoAcceptFolders: {star.AutoAcceptFolders}");

            Stars.AddOrUpdate(starId, star, (_, _) => star);
            Svc.Log.Debug($"[DEBUG] Star added to cache. Cache now contains {Stars.Count} stars");

            try
            {
                await PostNewStar(star);
                Svc.Log.Debug($"[DEBUG] Successfully posted Star to SyncThing API: {starId}");
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"[DEBUG] Failed to post Star to SyncThing API: {starId} - {ex.Message}");
                throw;
            }

            return star;
        }

        Svc.Log.Debug($"[DEBUG] Star already exists in cache: {starId}");
        return existingStar;
    }

    private async Task EnsurePairedStarsExist()
    {
        var effectivePairs = PsuPlugin.Configuration.GetEffectivePairs().ToList();
        Svc.Log.Debug($"[DEBUG] Checking {effectivePairs.Count} configured StarPacks for missing Stars...");
        Svc.Log.Debug($"[DEBUG] Current Stars in cache: {Stars.Count} - [{string.Join(", ", Stars.Keys)}]");

        foreach (var starPack in PsuPlugin.Configuration.GetAllStarPacks())
        {
            Svc.Log.Debug(
                $"[DEBUG] Checking StarPack - StarId: {starPack.StarId}, DataPackId: {starPack.DataPackId}");

            if (!Stars.ContainsKey(starPack.StarId))
            {
                Svc.Log.Debug($"[DEBUG] Star {starPack.StarId} not found in cache - adding to SyncThing");
                await EnsureStarInCache(starPack.StarId);
                Svc.Log.Debug($"[DEBUG] After adding: Stars in cache: {Stars.Count}");
            }
            else
            {
                Svc.Log.Debug($"[DEBUG] Star {starPack.StarId} already exists in cache");
            }
        }
    }

    private async Task ShareLocalDataPackWithPairedStars()
    {
        if (PsuPlugin.Configuration.MyStarPack == null)
        {
            Svc.Log.Warning("MyStarPack is null - cannot share local DataPack");
            return;
        }
        var myDataPack = PsuPlugin.Configuration.MyStarPack.GetDataPack();
        if (myDataPack == null)
        {
            Svc.Log.Warning($"MyStarPack DataPack not found: {PsuPlugin.Configuration.MyStarPack.DataPackId}");
            return;
        }
        bool modified = false;
        myDataPack.Stars ??= new List<Star>();

        var effectivePairs = PsuPlugin.Configuration.GetEffectivePairs().ToList();
        effectivePairs.Add(PsuPlugin.Configuration.MyStarPack); //Syncthing technically considers you to be sharing your own folder. Stupid....
        
        var currentStarIds = new HashSet<string>(myDataPack.Stars.Select(s => s.StarId));
        var expectedStarIds = new HashSet<string>(effectivePairs.Select(sp => sp.StarId));
        
        var starsToAdd = expectedStarIds.Except(currentStarIds).ToList();
        var starsToRemove = currentStarIds.Except(expectedStarIds).ToList();
        
        if (starsToRemove.Count == 0 && starsToAdd.Count == 0)
        {
            return; // Early exit - no changes needed
        }
        
        Svc.Log.Debug($"[DEBUG] Stars to add: {starsToAdd.Count}, Stars to remove: {starsToRemove.Count}");
        
        if (starsToRemove.Count > 0)
        {
            myDataPack.Stars.RemoveAll(s => starsToRemove.Contains(s.StarId));
            modified = true;
        }

        foreach (var starId in starsToAdd)
        {
            if (Stars.TryGetValue(starId, out var star))
            {
                myDataPack.Stars.Add(star);
                modified = true;
                Svc.Log.Debug($"[DEBUG] Added Star to MyDataPack: {star.StarId}");
            }
        }

        if (modified)
        {
            Svc.Log.Debug($"[DEBUG] Sending PUT request to SyncThing API for DataPack: {myDataPack.Id}");
            Svc.Log.Debug(
                $"[DEBUG] Final Stars list for MyDataPack: [{string.Join(", ", myDataPack.Stars.Select(s => s.StarId))}]");

            await _client!.Config.Folders.Put(myDataPack).ConfigureAwait(false);
            Svc.Log.Debug($"Updated local DataPack sharing with {myDataPack.Stars.Count} stars");
        }
        else
        {
            Svc.Log.Debug("[DEBUG] No modifications needed - DataPack already up to date");
        }
    }

    private async Task AcceptMatchingPendingFolders()
    {
        try
        {
            if (_client?.Config?.PendingFolders == null)
            {
                Svc.Log.Warning("PendingFolders client not available");
                return;
            }

            var pendingFolders = await _client.Config.PendingFolders.Get().ConfigureAwait(false);
            if (!pendingFolders.HasPendingFolders)
            {
                Svc.Log.Debug("No pending folders to process");
                return;
            }

            var effectivePairs = PsuPlugin.Configuration.GetEffectivePairs().ToList();

// Check each pending folder against our paired star DataPack IDs
            foreach (var kvp in pendingFolders.Folders)
            {
                var folderId = kvp.Key;
                var pendingFolder = kvp.Value;

// Check if this folder ID matches any of our paired stars' DataPack IDs
                var matchingStarPack = effectivePairs.FirstOrDefault(sp => sp.DataPackId.ToString() == folderId);
                if (matchingStarPack != null)
                {
                    Svc.Log.Debug($"Found matching pending folder from paired star: {folderId}");
                    await AcceptPendingFolder(folderId, pendingFolder);
                }
                else
                {
                    Svc.Log.Debug($"Ignoring pending folder '{folderId}': No matching StarPack configuration found");
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to process pending folders: {ex}");
        }
    }

    private async Task AcceptPendingFolder(string folderId, PendingFolder pendingFolder)
    {
        try
        {
            // Only accept folders with GUID-based IDs (our plugin's format)
            if (!Guid.TryParse(folderId, out var folderGuid))
            {
                Svc.Log.Debug(
                    $"Skipping pending folder '{folderId}': Not a GUID-based ID, likely from external SyncThing instance");
                return;
            }

            // Validate base directory for remote DataPacks
            var baseDir = PsuPlugin.Configuration.DefaultDataPackDirectory;
            if (string.IsNullOrWhiteSpace(baseDir))
            {
                Svc.Log.Warning($"Cannot accept pending folder {folderId}: DefaultDataPackDirectory is not set.");
                return;
            }

            // Ensure base directory exists
            Directory.CreateDirectory(baseDir);

            var canonicalId = folderGuid.ToString("D");

            var matchingStars = PsuPlugin.Configuration.GetAllStarPacks()
                .Where(sp => sp.DataPackId == folderGuid || pendingFolder.OfferedBy.ContainsKey(sp.StarId)).ToList()
                .Select(p => p.GetStar()!);

            // Create a receive-only DataPack for the remote user's data
            var dataPack = new DataPack(folderGuid)
            {
                Name = pendingFolder.OfferedBy.Values.First().Label,
                Path = Path.Combine(baseDir, canonicalId),
                Type = FolderType.ReceiveOnly,
                Stars = [.. matchingStars, PsuPlugin.Configuration.MyStarPack!.GetStar()],
            };

            // Add to cache and create in SyncThing API
            DataPacks.AddOrUpdate(folderGuid, dataPack, (_, _) => dataPack);
            await PostNewDataPack(dataPack);

            Svc.Log.Information($"Successfully accepted pending folder: {folderId}");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to accept pending folder {folderId}: {ex}");
        }
    }

    private void HealSyncThing()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                Svc.Log.Debug("Starting HealSyncThing...");

                // Step 1: Add all paired stars that don't exist in the API to the API
                await EnsurePairedStarsExist();

                // Step 2: Add all paired stars that DO exist in the API to the local (send only) DataPack
                await ShareLocalDataPackWithPairedStars();

                // Step 3: Accept any PendingFolder requests if their IDs match a paired star's DataPack ID
                await AcceptMatchingPendingFolders();

                Svc.Log.Debug("HealSyncThing completed successfully");
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"HealSyncThing failed: {ex}");
            }
        });
    }

    public void RemoveUnpairedStarsAndDataPacks()
    {
        _ = Task.Run(async () =>
        {
            if (!PsuPlugin.Configuration.UseBuiltInSyncThing)
            {
                Svc.Log.Debug("Easy Mode Disabled, cleanup must be manual");
                return;
            }
            if (PsuPlugin.Configuration.MyStarPack == null)
                return;
            var effectivePairs = PsuPlugin.Configuration.GetEffectivePairs().ToList();
            effectivePairs.Add(PsuPlugin.Configuration.MyStarPack);
            foreach (var dataPack in DataPacks.Values)
            {
                if (effectivePairs.All(sp => sp.DataPackId != dataPack.Id))
                {
                    Svc.Log.Debug($"[DEBUG] DataPack {dataPack.Id} is not paired with any configured StarPacks");
                    await RemoveDataPack(dataPack.Id);
                }
            }

            foreach (var star in Stars.Values)
            {
                if (effectivePairs.All(sp => sp.StarId != star.StarId))
                {
                    Svc.Log.Debug($"[DEBUG] Star {star.StarId} is not paired with any configured StarPacks");
                    await RemoveStar(star.StarId);
                }
            }
        });
    }

    private async Task RemoveStar(string starId)
    {
        try
        {
            await _client.Config.Stars.Delete(starId);
            Stars.TryRemove(starId, out _);
            Svc.Log.Debug($"Successfully removed star: {starId}");
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to remove star {starId}: {e}");
        }
    }
    
    private async Task RemoveDataPack(Guid dataPackId)
    {
        try
        {
            await _client.Config.Folders.Delete(dataPackId.ToString());
            DataPacks.TryRemove(dataPackId, out _);
            Svc.Log.Debug($"Successfully removed DataPack: {dataPackId}");
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to remove DataPack {dataPackId}: {e}");
        }
    }

    public async Task PostNewStar(Star star)
    {
        try
        {
            await _client.Config.Stars.Post(star).ConfigureAwait(false);
            Svc.Log.Debug($"Successfully posted new star: {star.StarId}");
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to post new star {star.StarId}: {e}");
        }
    }

    public async Task PostNewDataPack(DataPack dataPack)
    {
        try
        {
            if (_client?.Config?.Folders != null)
            {
                await _client.Config.Folders.Post(dataPack).ConfigureAwait(false);
                Svc.Log.Debug($"Successfully posted DataPack: {dataPack.Id}");
            }
            else
            {
                Svc.Log.Warning("Cannot post DataPack: SyncThing client not available");
            }
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to post DataPack {dataPack.Id}: {e}");
            throw; // Re-throw to let caller handle the failure
        }
    }

    public async Task PutDataPackMerged(DataPack local)
    {
        try
        {
            if (_client?.Config?.Folders == null)
            {
                Svc.Log.Warning("Cannot update DataPack: SyncThing client not available");
                return;
            }

            DataPack server;
            try
            {
                server = await _client.Config.Folders.Get(local.Id.ToString()).ConfigureAwait(false);
                if (server == null)
                {
                    Svc.Log.Warning($"Server DataPack {local.Id} not found; creating it.");
                    await _client.Config.Folders.Post(local).ConfigureAwait(false);
                    DataPacks.AddOrUpdate(local.Id, local, (_, _) => local);
                    return;
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"Failed to fetch DataPack {local.Id} from server, will attempt POST: {ex.Message}");
                await _client.Config.Folders.Post(local).ConfigureAwait(false);
                DataPacks.AddOrUpdate(local.Id, local, (_, _) => local);
                return;
            }

            // Merge editable fields only; preserve server Path and other unmanaged fields
            server.Name = local.Name;
            server.RescanIntervalS = local.RescanIntervalS;
            server.FsWatcherEnabled = local.FsWatcherEnabled;
            server.FsWatcherDelayS = local.FsWatcherDelayS;
            server.IgnorePerms = local.IgnorePerms;
            server.AutoNormalize = local.AutoNormalize;

            await _client.Config.Folders.Put(server).ConfigureAwait(false);

            DataPacks.AddOrUpdate(server.Id, server, (_, _) => server);

            Svc.Log.Debug($"Successfully updated DataPack (merged): {server.Id}");
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to update DataPack {local.Id} with merge: {e}");
        }
    }

    public void RefreshCaches()
    {
        _ = Task.Run(async () =>
        {
            if (_client == null)
                return;
            try
            {
                var ping = await _client.Config.System.Ping().ConfigureAwait(false);
                IsHealthy = ping.Ok;
                Svc.Log.Debug($"SyncThing API ping: {ping.Ok}");
                if (!IsHealthy)
                    return;

                // Connections and transfer rate computation
                Connections = await _client.Config.System.GetConnections().ConfigureAwait(false);
                if (Connections != null)
                {
                    foreach (var (starId, conn) in Connections.Connections)
                    {
                        var snapshot = new ByteSnapshot
                        {
                            InBytesTotal = conn.InBytesTotal,
                            OutBytesTotal = conn.OutBytesTotal,
                            At = conn.At,
                            Connected = conn.Connected,
                        };

                        if (_prevSnapshots.TryGetValue(starId, out var prev))
                        {
                            var dt = Math.Max(0.001, (snapshot.At - prev.At).TotalSeconds);
                            var inBps = (snapshot.InBytesTotal - prev.InBytesTotal) / dt;
                            var outBps = (snapshot.OutBytesTotal - prev.OutBytesTotal) / dt;
                            _currentRates[starId] = new TransferRates
                            {
                                InBps = inBps < 0 ? 0 : inBps,
                                OutBps = outBps < 0 ? 0 : outBps,
                                InBytesTotal = snapshot.InBytesTotal,
                                OutBytesTotal = snapshot.OutBytesTotal,
                                At = snapshot.At,
                                Connected = snapshot.Connected,
                                IsLocal = conn.IsLocal,
                            };
                        }

                        _prevSnapshots[starId] = snapshot;
                    }
                }

                // Folders - Only process folders with GUID-based IDs (PSU folders)
                if (_client?.Config.Folders != null)
                {
                    var allFolders = await _client.Config.Folders.Get();
                    foreach (var folder in allFolders)
                    {
                        // Only process folders with GUID-based IDs - ignore external SyncThing folders
                        if (Guid.TryParse(folder.IdString, out var guid))
                        {
                            DataPacks.AddOrUpdate(guid, folder, (_, _) => folder);
                        }
                        else
                        {
                            Svc.Log.Debug(
                                $"Ignoring non-GUID folder from external SyncThing instance: {folder.IdString}");
                        }
                    }
                }

                if (_client?.Config.Stars != null)
                {
                    // Stars
                    var stars = await _client.Config.Stars.Get();
                    foreach (var pair in stars)
                    {
                        Stars.AddOrUpdate(pair.StarId, pair, (_, _) => pair);
                    }
                }

                Svc.Log.Debug($"Refreshed caches | {DataPacks.Count} DataPacks | {Stars.Count} Stars");
                LastRefresh = DateTime.UtcNow;
                HealSyncThing();
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Failed to refresh caches: {e}");
                _client = null;
            }
        });
    }

    public void CleanLocalDataPack()
    {
        try
        {
            const long giggleBit = 1_073_741_824L;
            var maxSize = PsuPlugin.Configuration.MaxDataPackSizeGb;
            var minSize = PsuPlugin.PlayerDataService.LocalPlayerData?.MinimumRequiredDataPackSize;
            if ((maxSize * giggleBit) <
                minSize)
            {
                Svc.Log.Error(
                    $"Your configured maximum data pack size of {maxSize} GB is too small to hold your character's current size of {minSize} bytes. Please increase the limit in the settings tab.");
                return;
            }

            var myPack = PsuPlugin.Configuration.MyStarPack?.GetDataPack();
            if (myPack == null)
                return;
            var files = Directory.GetFiles(myPack.FilesPath, "*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastAccessTimeUtc)
                .ToList();
            var size = files.Sum(f => f.Length);
            if (size > maxSize * giggleBit)
            {
                Svc.Log.Information(
                    $"My DataPack size {size / giggleBit:N2} GB exceeds limit of {maxSize} GB, cleaning up...");
                var targetSize = maxSize * giggleBit * 0.9;
                foreach (var file in files)
                {
                    try
                    {
                        if (size <= targetSize)
                            break;
                        size -= file.Length;
                        file.Delete();
                    }
                    catch (Exception e)
                    {
                        Svc.Log.Warning($"Failed to delete file {file.FullName}: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to clean local DataPack: {e}");
        }
    }

    public bool IsStarOnline(string starId)
    {
        try
        {
            var conn = Connections?.Connections.GetValueOrDefault(starId);
            return conn?.Connected ?? false;
        }
        catch
        {
            return false;
        }
    }

    public void Shutdown()
    {
        _client?.Config.System.Shutdown().ConfigureAwait(false).GetAwaiter().GetResult();
        Svc.Log.Information("SyncThing Instance Shutdown Complete");
    }

    public TransferRates? GetTransferRates(string starId)
    {
        return _currentRates.TryGetValue(starId, out var v) ? v : null;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= LocalUpdate;
    }
}