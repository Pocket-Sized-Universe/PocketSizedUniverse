using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using PocketSizedUniverse.Models.Mods;

namespace PocketSizedUniverse.Models.Data;

public class LocalPlayerData : PlayerData
{
    public LocalPlayerData(StarPack myStarPack) : base(myStarPack)
    {
        var localPlayer = Svc.Framework.RunOnFrameworkThread(() => Svc.ClientState.LocalPlayer).Result;
        if (localPlayer == null)
        {
            Svc.Log.Error("Failed to get local player.");
            throw new InvalidOperationException("Failed to get local player.");
        }

        Player = localPlayer;
    }

    // Serialize writes and coalesce rapid triggers
    private readonly SemaphoreSlim _basicWriteLock = new(1, 1);
    private readonly SemaphoreSlim _glamWriteLock = new(1, 1);
    private readonly SemaphoreSlim _penumbraWriteLock = new(1, 1);
    private readonly SemaphoreSlim _customizeWriteLock = new(1, 1);
    private readonly SemaphoreSlim _honorificWriteLock = new(1, 1);

    // Debounce to coalesce rapid triggers per data type
    private const int DebounceMs = 150;
    private int _basicScheduled;
    private int _glamScheduled;
    private int _penumbraScheduled;
    private int _customizeScheduled;
    private int _honorificScheduled;

    private static async Task AtomicWriteTextAsync(string path, string content, int maxAttempts = 8,
        int initialDelayMs = 25)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        // Write to a unique temp file first, in the same directory
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        await using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                         FileOptions.Asynchronous))
        {
            await fs.WriteAsync(bytes, 0, bytes.Length);
            await fs.FlushAsync();
        }

        // Try to atomically replace/rename into place with retries
        int delay = initialDelayMs;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Replace(tmp, path, null);
                }
                else
                {
                    // Destination not present yet; rename into place
                    File.Move(tmp, path);
                }
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(delay);
                delay = Math.Min(delay * 2, 1000);
            }
        }

        // Final attempt; let exceptions surface, but try cleanup temp on failure
        try
        {
            if (File.Exists(path))
                File.Replace(tmp, path, null);
            else
                File.Move(tmp, path);
        }
        finally
        {
            try
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    public async Task UpdateHonorificData()
    {
        if (Interlocked.Exchange(ref _honorificScheduled, 1) == 1)
            return;
        try
        {
            await Task.Delay(DebounceMs);

            if (Player == null)
                return;
            var honorific = Svc.Framework.RunOnFrameworkThread(() => PsuPlugin.HonorificService.GetLocalCharacterTitle()).Result ?? string.Empty;
            var honorificData = new HonorificData()
            {
                LastUpdatedUtc = DateTime.UtcNow,
                Title = honorific
            };

            try
            {
                await _customizeWriteLock.WaitAsync();
                var changed = HonorificData == null ||
                              !string.Equals(HonorificData.Title, honorificData.Title, StringComparison.Ordinal);
                if (changed)
                {
                    HonorificData = honorificData;
                    var cLoc = HonorificData.GetPath(StarPackReference.GetDataPack()!.DataPath);
                    var encodedHonorific = Base64Util.ToBase64(HonorificData);
                    await AtomicWriteTextAsync(cLoc, encodedHonorific);
                    Svc.Log.Debug("Updated Honorific data on disk.");
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Error writing Honorific data to disk: {ex}");
            }
            finally
            {
                _customizeWriteLock.Release();
            }
        }
        finally
        {
            Volatile.Write(ref _honorificScheduled, 0);
        }
    }

    public async Task UpdateCustomizeData()
    {
        if (Interlocked.Exchange(ref _customizeScheduled, 1) == 1)
            return;
        try
        {
            await Task.Delay(DebounceMs);

            if (Player == null)
                return;
            string data = string.Empty;
            var activeProfileId = PsuPlugin.CustomizeService.GetActiveProfileOnCharacter(Player.ObjectIndex);
            if (activeProfileId.Item1 > 0 || activeProfileId.Item2 == null || activeProfileId.Item2 == Guid.Empty)
            {
                Svc.Log.Debug("Failed to get active C+ profile.");
            }
            else
            {
                var customizeData = PsuPlugin.CustomizeService.GetCustomizeProfileByUniqueId(activeProfileId.Item2.Value);
                if (customizeData.Item1 > 0 || string.IsNullOrEmpty(customizeData.Item2))
                {
                    Svc.Log.Warning("Failed to get customize data.");
                }
                else
                {
                    data = customizeData.Item2;
                }
            }


            var cData = new CustomizeData()
            {
                CustomizeState = data,
                LastUpdatedUtc = DateTime.UtcNow
            };

            try
            {
                await _customizeWriteLock.WaitAsync();
                var changed = CustomizeData == null ||
                              !string.Equals(CustomizeData.CustomizeState, cData.CustomizeState, StringComparison.Ordinal);
                if (changed)
                {
                    CustomizeData = cData;
                    var cLoc = CustomizeData.GetPath(StarPackReference.GetDataPack()!.DataPath);
                    var encodedCustomize = Base64Util.ToBase64(CustomizeData);
                    await AtomicWriteTextAsync(cLoc, encodedCustomize);
                    Svc.Log.Debug("Updated Customize+ data on disk.");
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Error writing Customize+ data to disk: {ex}");
            }
            finally
            {
                _customizeWriteLock.Release();
            }
        }
        finally
        {
            Volatile.Write(ref _customizeScheduled, 0);
        }
    }

    public async Task UpdateGlamData()
    {
        // Debounce: if a run is scheduled/in-flight, skip; otherwise coalesce triggers
        if (Interlocked.Exchange(ref _glamScheduled, 1) == 1)
            return;
        try
        {
            await Task.Delay(DebounceMs);

            // Glamourer
            var glamState = PsuPlugin.GlamourerService.GetStateBase64.Invoke(Player.ObjectIndex);
            if (glamState.Item2 == null)
            {
                Svc.Log.Warning("Failed to get glamourer state.");
                return;
            }

            var glamData = new GlamourerData()
            {
                GlamState = glamState.Item2
            };
            // Save Glamourer data if changed
            // Acquire lock and re-check to coalesce concurrent triggers
            try
            {
                await _glamWriteLock.WaitAsync();
                var changed = GlamourerData == null ||
                              !string.Equals(GlamourerData.GlamState, glamData.GlamState, StringComparison.Ordinal);
                if (changed)
                {
                    GlamourerData = glamData;
                    var glamourerLoc = GlamourerData.GetPath(StarPackReference.GetDataPack()!.DataPath);
                    var encodedGlamourer = Base64Util.ToBase64(GlamourerData);
                    await AtomicWriteTextAsync(glamourerLoc, encodedGlamourer);
                    Svc.Log.Debug("Updated glamourer data on disk.");
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Error writing Glamourer data to disk: {ex}");
            }
            finally
            {
                _glamWriteLock.Release();
            }
        }
        finally
        {
            Volatile.Write(ref _glamScheduled, 0);
        }
    }

    public async Task UpdateBasicData()
    {
        if (Interlocked.Exchange(ref _basicScheduled, 1) == 1)
            return;
        try
        {
            await Task.Delay(DebounceMs);

            var newBasic = await Svc.Framework.RunOnFrameworkThread(() => new BasicData
            {
                PlayerName = Player.Name.TextValue,
                WorldId = Player.HomeWorld.RowId
            });
            try
            {
                await _basicWriteLock.WaitAsync();
                var changed = Data?.PlayerName != newBasic.PlayerName || Data?.WorldId != newBasic.WorldId;
                if (changed)
                {
                    Data = newBasic;
                    var playerDataLoc = Data.GetPath(StarPackReference.GetDataPack()!.DataPath);
                    var encodedData = Base64Util.ToBase64(Data);
                    await AtomicWriteTextAsync(playerDataLoc, encodedData);
                    Svc.Log.Debug("Updated basic info on disk.");
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Error writing basic data to disk: {ex}");
            }
            finally
            {
                _basicWriteLock.Release();
            }
        }
        finally
        {
            Volatile.Write(ref _basicScheduled, 0);
        }
    }

    public async Task UpdatePenumbraData()
    {
        if (Interlocked.Exchange(ref _penumbraScheduled, 1) == 1)
            return;
        try
        {
            await Task.Delay(DebounceMs);

            var penumbra = new PenumbraData();
            var manips = PsuPlugin.PenumbraService.GetPlayerMetaManipulations.Invoke();

            // Resolve current character resource paths (local file -> game paths)
            var resourcePaths = Svc.Framework.RunOnFrameworkThread(() =>
                PsuPlugin.PenumbraService.GetGameObjectResourcePaths.Invoke(Player.ObjectIndex)).Result;
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
            try
            {
                await _penumbraWriteLock.WaitAsync();
                var penumbraChanged =
                    PenumbraData == null
                    || !string.Equals(PenumbraData.MetaManipulations, penumbra.MetaManipulations, StringComparison.Ordinal)
                    || !UnorderedEqualByKey(PenumbraData.Files, penumbra.Files, FileKey)
                    || !UnorderedEqualByKey(PenumbraData.FileSwaps, penumbra.FileSwaps, SwapKey);
                if (penumbraChanged)
                {
                    PenumbraData = penumbra;
                    var penumbraLoc = PenumbraData.GetPath(StarPackReference.GetDataPack()!.DataPath);
                    var encodedPenumbra = Base64Util.ToBase64(PenumbraData);
                    await AtomicWriteTextAsync(penumbraLoc, encodedPenumbra);
                    Svc.Log.Debug("Updated penumbra data on disk.");
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Error writing penumbra data to disk: {ex}");
            }
            finally
            {
                _penumbraWriteLock.Release();
            }
        }
        finally
        {
            Volatile.Write(ref _penumbraScheduled, 0);
        }
    }

    public sealed override IPlayerCharacter? Player { get; set; }
}