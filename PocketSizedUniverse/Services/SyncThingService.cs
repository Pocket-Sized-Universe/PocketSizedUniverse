using System.Collections.Concurrent;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Models.Syncthing.Models.Response;
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
        Svc.Framework.Update += Update;
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

    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(10);
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;

    public void Update(IFramework framework)
    {
        // Periodically refresh caches and stats
        if (DateTime.Now - LastUpdated >= UpdateInterval)
        {
            LastUpdated = DateTime.Now;
            RefreshCaches();
        }

        // Invalidate caches if it's been a while to ensure we reconcile with config changes
        if (TimeSpan.FromMinutes(1) < DateTime.Now - LastRefresh)
        {
            InvalidateCaches();
        }
    }

    public void InvalidateCaches()
    {
        DataPacks.Clear();
        Stars.Clear();
        InitializeClient(); // Reinitialize client with updated configuration
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


    private IEnumerable<StarPack> GetEffectivePairs()
    {
        var blocked =
            new HashSet<(string, Guid)>(PsuPlugin.Configuration.Blocklist.Select(b => (b.StarId, b.DataPackId)));
        return PsuPlugin.Configuration.StarPacks.Where(sp => !blocked.Contains((sp.StarId, sp.DataPackId)));
    }

    private async Task EnsurePairedStarsExist()
    {
        var effectivePairs = GetEffectivePairs().ToList();
        Svc.Log.Debug($"[DEBUG] Checking {effectivePairs.Count} configured StarPacks for missing Stars...");
        Svc.Log.Debug($"[DEBUG] Current Stars in cache: {Stars.Count} - [{string.Join(", ", Stars.Keys)}]");

        foreach (var starPack in PsuPlugin.Configuration.StarPacks)
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

        Svc.Log.Debug(
            $"[DEBUG] MyStarPack - StarId: {PsuPlugin.Configuration.MyStarPack.StarId}, DataPackId: {PsuPlugin.Configuration.MyStarPack.DataPackId}");

        var myDataPack = PsuPlugin.Configuration.MyStarPack.GetDataPack();
        if (myDataPack == null)
        {
            Svc.Log.Warning($"MyStarPack DataPack not found: {PsuPlugin.Configuration.MyStarPack.DataPackId}");
            return;
        }

        Svc.Log.Debug(
            $"[DEBUG] MyDataPack - Id: {myDataPack.Id}, Type: {myDataPack.Type}, Path: {myDataPack.Path}");
        Svc.Log.Debug($"[DEBUG] MyDataPack current Stars count: {myDataPack.Stars?.Count ?? 0}");

        if (myDataPack.Stars != null)
        {
            foreach (var existingStar in myDataPack.Stars)
            {
                Svc.Log.Debug($"[DEBUG] MyDataPack existing Star: {existingStar.StarId}");
            }
        }

        bool modified = false;
        myDataPack.Stars ??= new List<Star>();

        var effectivePairs = GetEffectivePairs().ToList();
        Svc.Log.Debug($"[DEBUG] Processing {effectivePairs.Count} configured StarPacks...");

// Add all paired stars that exist in the API to the local DataPack
        foreach (var starPack in effectivePairs)
        {
            Svc.Log.Debug(
                $"[DEBUG] Processing StarPack - StarId: {starPack.StarId}, DataPackId: {starPack.DataPackId}");

            if (Stars.TryGetValue(starPack.StarId, out var star))
            {
                Svc.Log.Debug(
                    $"[DEBUG] Found Star in cache - StarId: {star.StarId}, Name: {star.Name ?? "<null>"}, AutoAcceptFolders: {star.AutoAcceptFolders}");

                if (myDataPack.Stars.All(s => s.StarId != star.StarId))
                {
                    myDataPack.Stars.Add(star);
                    modified = true;
                    Svc.Log.Debug($"[DEBUG] Added Star to MyDataPack: {star.StarId}");
                }
                else
                {
                    Svc.Log.Debug($"[DEBUG] Star already in MyDataPack: {star.StarId}");
                }
            }
            else
            {
                Svc.Log.Warning($"[DEBUG] Star NOT found in cache: {starPack.StarId}");
                Svc.Log.Debug($"[DEBUG] Available Stars in cache: {string.Join(", ", Stars.Keys)}");
            }
        }

        if (modified)
        {
            Svc.Log.Debug($"[DEBUG] Sending PUT request to SyncThing API for DataPack: {myDataPack.Id}");
            Svc.Log.Debug(
                $"[DEBUG] Final Stars list for MyDataPack: [{string.Join(", ", myDataPack.Stars.Select(s => s.StarId))}]");

            await _client!.Config.Folders.Put(myDataPack).ConfigureAwait(false);
            Svc.Log.Debug($"Updated local DataPack sharing with {myDataPack.Stars.Count} stars");

            // Let's also verify the update took effect by reading it back
            try
            {
                var updatedDataPack = await _client.Config.Folders.Get(myDataPack.Id.ToString()).ConfigureAwait(false);
                Svc.Log.Debug(
                    $"[DEBUG] Verified - Updated DataPack has {updatedDataPack.Stars?.Count ?? 0} stars after PUT");
                if (updatedDataPack.Stars != null)
                {
                    foreach (var verifiedStar in updatedDataPack.Stars)
                    {
                        Svc.Log.Debug($"[DEBUG] Verified Star in DataPack: {verifiedStar.StarId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"[DEBUG] Failed to verify DataPack update: {ex.Message}");
            }
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

            var effectivePairs = GetEffectivePairs().ToList();
            var effectiveDataPackIds = new HashSet<Guid>(effectivePairs.Select(p => p.DataPackId));

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

            var matchingStars = PsuPlugin.Configuration.StarPacks
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
                    var allFolders = _client.Config.Folders.Get().ConfigureAwait(false).GetAwaiter().GetResult();
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

                // Stars
                var stars = _client?.Config.Stars.Get().ConfigureAwait(false).GetAwaiter().GetResult();
                foreach (var pair in stars)
                {
                    Stars.AddOrUpdate(pair.StarId, pair, (_, _) => pair);
                }

                Svc.Log.Debug($"Refreshed caches | {DataPacks.Count} DataPacks | {Stars.Count} Stars");
                LastRefresh = DateTime.Now;
                HealSyncThing();
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Failed to refresh caches: {e}");
                _client = null;
            }
        });
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

    public TransferRates? GetTransferRates(string starId)
    {
        return _currentRates.TryGetValue(starId, out var v) ? v : null;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= Update;
    }
}