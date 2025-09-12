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

    private void HealSyncThing()
    {
        _ = Task.Run(async () =>
        {
            foreach (var pack in PsuPlugin.Configuration.StarPacks)
            {
                if (!Stars.TryGetValue(pack.StarId, out var existingStar))
                {
                    Svc.Log.Information("New Star found!");
                    var star = new Star()
                    {
                        StarId = pack.StarId,
                        Name = $"Star-{pack.StarId[..8]}",
                        Compression = "metadata",
                        AutoAcceptFolders = true
                    };

                    // Add to cache and post to API
                    Stars.AddOrUpdate(pack.StarId, star, (_, _) => star);
                    await PostNewStar(star);
                    existingStar = star;
                }

                if (!DataPacks.TryGetValue(pack.DataPackId, out var existingPack))
                {
                    Svc.Log.Information("New DataPack found!");
                    var dataPack = new DataPack(pack.DataPackId)
                    {
                        Name = $"DataPack-{pack.DataPackId}",
                        Path = Path.Combine(PsuPlugin.Configuration.DefaultDataPackDirectory!,
                            pack.DataPackId.ToString()),
                        Type = FolderType.Receiveonly,
                        RescanIntervalS = 3600,
                        FsWatcherEnabled = true,
                        FsWatcherDelayS = 10,
                        IgnorePerms = false,
                        AutoNormalize = true,
                        Stars = new List<Star> { existingStar },
                        MinDiskFree = new MinDiskFree()
                    };

                    dataPack.EnsureFolders();

                    // Add to cache and post to API
                    DataPacks.AddOrUpdate(pack.DataPackId, dataPack, (_, _) => dataPack);
                    PostNewDataPack(dataPack);
                    existingPack = dataPack;
                }
                else
                {
                    Svc.Log.Information("Star and DataPack exist, checking associations");

                    // Check if the star is properly associated with the data pack
                    bool needsUpdate = false;
                    if (existingPack.Stars.All(s => s.StarId != existingStar.StarId))
                    {
                        existingPack.Stars.Add(existingStar);
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        Svc.Log.Information("Updating DataPack associations");
                        PostNewDataPack(existingPack);
                    }
                }

                // Ensure the star is included in MyStarPack
                if (PsuPlugin.Configuration.MyStarPack != null)
                {
                    var myDataPack = PsuPlugin.Configuration.MyStarPack.GetDataPack();
                    if (myDataPack != null)
                    {
                        bool myPackNeedsUpdate = false;
                        if (myDataPack.Stars.All(s => s.StarId != existingStar.StarId))
                        {
                            myDataPack.Stars.Add(existingStar);
                            myPackNeedsUpdate = true;
                        }

                        if (myPackNeedsUpdate)
                        {
                            Svc.Log.Information("Adding star to personal data pack");
                            PostNewDataPack(myDataPack);
                        }
                    }
                }
            }
            IsHealthy = true;
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

    public void PostNewDataPack(DataPack dataPack)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _client.Config.Folders.Post(dataPack).ConfigureAwait(false);
                Svc.Log.Information($"Successfully posted DataPack: {dataPack.Id}");
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Failed to post DataPack {dataPack.Id}: {e}");
            }
        });
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