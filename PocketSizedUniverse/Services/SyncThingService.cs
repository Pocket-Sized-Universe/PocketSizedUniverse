using System.Collections.Concurrent;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models;
using Syncthing;
using Syncthing.Http;
using Syncthing.Models.Response;

namespace PocketSizedUniverse.Services;

public class SyncThingService : ICache
{
    public ConcurrentDictionary<string, Star> Stars { get; } = new();
    public ConcurrentDictionary<Guid, DataPack> DataPacks { get; } = new();

    private SyncthingClient? _client;
    public bool IsHealthy { get; private set; } = false;
    public DateTime LastRefresh { get; private set; } = DateTime.MinValue;

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
        if (TimeSpan.FromMinutes(1) < DateTime.Now - LastRefresh)
        {
            InvalidateCaches();
        }
    }

    public void InvalidateCaches()
    {
        DataPacks.Clear();
        Stars.Clear();
        IsHealthy = false;
        InitializeClient(); // Reinitialize client with updated configuration
        RefreshCaches();
    }

    private async Task<Star> EnsureStarInCache(string starId)
    {
        if (!Stars.TryGetValue(starId, out var existingStar))
        {
            Svc.Log.Information($"[DEBUG] Creating new Star: {starId}");
            var star = new Star()
            {
                StarId = starId,
                AutoAcceptFolders = false,
            };
            
            Svc.Log.Information($"[DEBUG] Star object created - StarId: {star.StarId}, Name: {star.Name}, AutoAcceptFolders: {star.AutoAcceptFolders}");
            
            Stars.AddOrUpdate(starId, star, (_, _) => star);
            Svc.Log.Information($"[DEBUG] Star added to cache. Cache now contains {Stars.Count} stars");
            
            try
            {
                await PostNewStar(star);
                Svc.Log.Information($"[DEBUG] Successfully posted Star to SyncThing API: {starId}");
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"[DEBUG] Failed to post Star to SyncThing API: {starId} - {ex.Message}");
                throw;
            }
            
            return star;
        }
        
        Svc.Log.Information($"[DEBUG] Star already exists in cache: {starId}");
        return existingStar;
    }
    
    
    private async Task EnsurePairedStarsExist()
    {
        Svc.Log.Information($"[DEBUG] Checking {PsuPlugin.Configuration.StarPacks.Count} configured StarPacks for missing Stars...");
        Svc.Log.Information($"[DEBUG] Current Stars in cache: {Stars.Count} - [{string.Join(", ", Stars.Keys)}]");
        
        foreach (var starPack in PsuPlugin.Configuration.StarPacks)
        {
            Svc.Log.Information($"[DEBUG] Checking StarPack - StarId: {starPack.StarId}, DataPackId: {starPack.DataPackId}");
            
            if (!Stars.ContainsKey(starPack.StarId))
            {
                Svc.Log.Information($"[DEBUG] Star {starPack.StarId} not found in cache - adding to SyncThing");
                await EnsureStarInCache(starPack.StarId);
                Svc.Log.Information($"[DEBUG] After adding: Stars in cache: {Stars.Count}");
            }
            else
            {
                Svc.Log.Information($"[DEBUG] Star {starPack.StarId} already exists in cache");
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
        
        Svc.Log.Information($"[DEBUG] MyStarPack - StarId: {PsuPlugin.Configuration.MyStarPack.StarId}, DataPackId: {PsuPlugin.Configuration.MyStarPack.DataPackId}");
        
        var myDataPack = PsuPlugin.Configuration.MyStarPack.GetDataPack();
        if (myDataPack == null)
        {
            Svc.Log.Warning($"MyStarPack DataPack not found: {PsuPlugin.Configuration.MyStarPack.DataPackId}");
            return;
        }
        
        Svc.Log.Information($"[DEBUG] MyDataPack - Id: {myDataPack.Id}, Type: {myDataPack.Type}, Path: {myDataPack.Path}");
        Svc.Log.Information($"[DEBUG] MyDataPack current Stars count: {myDataPack.Stars?.Count ?? 0}");
        
        if (myDataPack.Stars != null)
        {
            foreach (var existingStar in myDataPack.Stars)
            {
                Svc.Log.Information($"[DEBUG] MyDataPack existing Star: {existingStar.StarId}");
            }
        }
        
        bool modified = false;
        myDataPack.Stars ??= new List<Star>();
        
        Svc.Log.Information($"[DEBUG] Processing {PsuPlugin.Configuration.StarPacks.Count} configured StarPacks...");
        
        // Add all paired stars that exist in the API to the local DataPack
        foreach (var starPack in PsuPlugin.Configuration.StarPacks)
        {
            Svc.Log.Information($"[DEBUG] Processing StarPack - StarId: {starPack.StarId}, DataPackId: {starPack.DataPackId}");
            
            if (Stars.TryGetValue(starPack.StarId, out var star))
            {
                Svc.Log.Information($"[DEBUG] Found Star in cache - StarId: {star.StarId}, Name: {star.Name ?? "<null>"}, AutoAcceptFolders: {star.AutoAcceptFolders}");
                
                if (myDataPack.Stars.All(s => s.StarId != star.StarId))
                {
                    myDataPack.Stars.Add(star);
                    modified = true;
                    Svc.Log.Information($"[DEBUG] Added Star to MyDataPack: {star.StarId}");
                }
                else
                {
                    Svc.Log.Information($"[DEBUG] Star already in MyDataPack: {star.StarId}");
                }
            }
            else
            {
                Svc.Log.Warning($"[DEBUG] Star NOT found in cache: {starPack.StarId}");
                Svc.Log.Information($"[DEBUG] Available Stars in cache: {string.Join(", ", Stars.Keys)}");
            }
        }
        
        if (modified)
        {
            Svc.Log.Information($"[DEBUG] Sending PUT request to SyncThing API for DataPack: {myDataPack.Id}");
            Svc.Log.Information($"[DEBUG] Final Stars list for MyDataPack: [{string.Join(", ", myDataPack.Stars.Select(s => s.StarId))}]");
            
            await _client!.Config.Folders.Put(myDataPack).ConfigureAwait(false);
            Svc.Log.Information($"Updated local DataPack sharing with {myDataPack.Stars.Count} stars");
            
            // Let's also verify the update took effect by reading it back
            try
            {
                var updatedDataPack = await _client.Config.Folders.Get(myDataPack.Id.ToString()).ConfigureAwait(false);
                Svc.Log.Information($"[DEBUG] Verified - Updated DataPack has {updatedDataPack.Stars?.Count ?? 0} stars after PUT");
                if (updatedDataPack.Stars != null)
                {
                    foreach (var verifiedStar in updatedDataPack.Stars)
                    {
                        Svc.Log.Information($"[DEBUG] Verified Star in DataPack: {verifiedStar.StarId}");
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
            Svc.Log.Information("[DEBUG] No modifications needed - DataPack already up to date");
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
            
            // Check each pending folder against our paired star DataPack IDs
            foreach (var kvp in pendingFolders.Folders)
            {
                var folderId = kvp.Key;
                var pendingFolder = kvp.Value;
                
                // Check if this folder ID matches any of our paired stars' DataPack IDs
                var matchingStarPack = PsuPlugin.Configuration.StarPacks.FirstOrDefault(sp => sp.DataPackId.ToString() == folderId);
                if (matchingStarPack != null)
                {
                    Svc.Log.Information($"Found matching pending folder from paired star: {folderId}");
                    await AcceptPendingFolder(folderId, pendingFolder);
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
            // To accept a pending folder, we create a new DataPack with the same ID
            // SyncThing will automatically link it to the pending invitation
            var folderGuid = Guid.Parse(folderId);
            var matchingStars = PsuPlugin.Configuration.StarPacks.Where(sp => sp.DataPackId == folderGuid || pendingFolder.OfferedBy.ContainsKey(sp.StarId)).ToList().Select(p => p.GetStar()!);
            
            // Create a receive-only DataPack for the remote user's data
            var dataPack = new DataPack(folderGuid)
            {
                Name = pendingFolder.OfferedBy.Values.First().Label,
                Path = Path.Combine(PsuPlugin.Configuration.DefaultDataPackDirectory!, folderId),
                Type = FolderType.Receiveonly,
                Stars = [ .. matchingStars, PsuPlugin.Configuration.MyStarPack!.GetStar()],
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
                Svc.Log.Information("Starting HealSyncThing...");
                
                // Step 1: Add all paired stars that don't exist in the API to the API
                await EnsurePairedStarsExist();
                
                // Step 2: Add all paired stars that DO exist in the API to the local (send only) DataPack
                await ShareLocalDataPackWithPairedStars();
                
                // Step 3: Accept any PendingFolder requests if their IDs match a paired star's DataPack ID
                await AcceptMatchingPendingFolders();

                IsHealthy = true;
                Svc.Log.Information("HealSyncThing completed successfully");
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"HealSyncThing failed: {ex}");
                IsHealthy = false;
            }
        });
    }

    public async Task PostNewStar(Star star)
    {
        try
        {
            await _client.Config.Stars.Post(star).ConfigureAwait(false);
            Svc.Log.Information($"Successfully posted new star: {star.StarId}");
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
                Svc.Log.Information($"Successfully posted DataPack: {dataPack.Id}");
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

    public void RefreshCaches()
    {
        _ = Task.Run(() =>
        {
            if (_client == null)
                return;
            try
            {
                if (_client?.Config.Folders != null)
                {
                    var constellations = _client.Config.Folders.Get().ConfigureAwait(false).GetAwaiter().GetResult();
                    foreach (var galaxy in constellations)
                    {
                        DataPacks.AddOrUpdate(galaxy.Id, galaxy, (_, _) => galaxy);
                    }
                }

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
                IsHealthy = false;
            }
        });
    }
}