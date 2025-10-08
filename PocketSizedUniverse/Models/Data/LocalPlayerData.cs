using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.Configuration;
using ECommons.DalamudServices;
using PocketSizedUniverse.Models.Mods;

namespace PocketSizedUniverse.Models.Data;

public class LocalPlayerData : PlayerData
{
    public LocalPlayerData(StarPack myStarPack) : base(myStarPack)
    {
        var maxParallel = Math.Max(2, Environment.ProcessorCount - 1);
        _semaphoreSlim = new SemaphoreSlim(maxParallel);
        _penumbraCts = new CancellationTokenSource();
    }

    private DateTime _lastMoodlesUpdate = DateTime.MinValue;
    private DateTime _lastHonorificUpdate = DateTime.MinValue;
    private DateTime _lastCustomizeUpdate = DateTime.MinValue;
    private DateTime _lastGlamUpdate = DateTime.MinValue;
    private DateTime _lastBasicUpdate = DateTime.MinValue;
    private DateTime _lastPenumbraUpdate = DateTime.MinValue;
    private DateTime _lastHeelsUpdate = DateTime.MinValue;
    private DateTime _lastPetNameUpdate = DateTime.MinValue;
    private readonly SemaphoreSlim _semaphoreSlim;

    // Penumbra heavy compute job control
    private readonly CancellationTokenSource _penumbraCts;

    private static readonly ConcurrentDictionary<string, (DateTime LastWriteUtc, long Length, byte[] Hash)>
        FileHashCache = new(StringComparer.OrdinalIgnoreCase);

    private async Task WriteText(string path, string content)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(path, content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error writing to {path}: {ex}");
        }
    }

    public void UpdateMoodlesData()
    {
        try
        {
            if (Player == null)
                return;
            if (DateTime.UtcNow - _lastMoodlesUpdate < TimeSpan.FromMilliseconds(500))
                return;
            _lastMoodlesUpdate = DateTime.UtcNow;
            var moodles = PsuPlugin.MoodlesService.GetStatusManager(Player.Address);
            var pack = StarPackReference.GetDataPack();
            if (pack == null) return;

            var moodlesData = new MoodlesData()
            {
                LastUpdatedUtc = DateTime.UtcNow,
                MoodlesState = moodles
            };

            var changed = MoodlesData == null ||
                          !string.Equals(MoodlesData.MoodlesState, moodlesData.MoodlesState, StringComparison.Ordinal);
            if (changed)
            {
                MoodlesData = moodlesData;
                var cLoc = MoodlesData.GetPath(pack.DataPath);
                var encodedMoodles = Base64Util.ToBase64(MoodlesData);
                Task.Run(async () =>
                {
                    await WriteText(cLoc, encodedMoodles);
                    Svc.Log.Debug("Updated Moodles data on disk.");
                });
            }
        }
        catch (IpcNotReadyError ex)
        {
            Svc.Log.Warning("Moodles plugin not ready.");
        }
    }

    public void UpdatePetNameData()
    {
        try
        {
            if (Player == null)
                return;
            if (DateTime.UtcNow - _lastPetNameUpdate < TimeSpan.FromMilliseconds(500))
                return;
            _lastPetNameUpdate = DateTime.UtcNow;

            var petName = PsuPlugin.PetNameService.GetPlayerData();
            var nameData = new PetNameData()
            {
                LastUpdatedUtc = DateTime.UtcNow,
                PetName = petName
            };
            var pack = StarPackReference.GetDataPack();
            if (pack == null) return;

            var changed = PetNameData == null ||
                          !string.Equals(PetNameData.PetName, nameData.PetName, StringComparison.Ordinal);
            if (changed)
            {
                PetNameData = nameData;
                var cLoc = PetNameData.GetPath(pack.DataPath);
                var encodedPetName = Base64Util.ToBase64(PetNameData);
                Task.Run(async () =>
                    {
                        await WriteText(cLoc, encodedPetName);
                        Svc.Log.Debug("Updated Pet Name data on disk.");
                    }
                );
            }
        }
        catch (IpcNotReadyError ex)
        {
            Svc.Log.Warning("Pet Renamer plugin not ready.");
        }
    }

    public void UpdateHonorificData()
    {
        try
        {
            if (Player == null)
                return;
            if (DateTime.UtcNow - _lastHonorificUpdate < TimeSpan.FromMilliseconds(500))
                return;
            _lastHonorificUpdate = DateTime.UtcNow;
            var honorific = PsuPlugin.HonorificService.GetLocalCharacterTitle() ?? string.Empty;
            var honorificData = new HonorificData()
            {
                LastUpdatedUtc = DateTime.UtcNow,
                Title = honorific
            };
            var pack = StarPackReference.GetDataPack();
            if (pack == null) return;

            var changed = HonorificData == null ||
                          !string.Equals(HonorificData.Title, honorificData.Title, StringComparison.Ordinal);
            if (changed)
            {
                HonorificData = honorificData;
                var cLoc = HonorificData.GetPath(pack.DataPath);
                var encodedHonorific = Base64Util.ToBase64(HonorificData);
                Task.Run(async () =>
                {
                    await WriteText(cLoc, encodedHonorific);
                    Svc.Log.Debug("Updated Honorific data on disk.");
                });
            }
        }
        catch (IpcNotReadyError ex)
        {
            Svc.Log.Warning("Honorific plugin not ready.");
        }
    }

    public void UpdateCustomizeData()
    {
        try
        {
            if (Player == null)
                return;
            if (DateTime.UtcNow - _lastCustomizeUpdate < TimeSpan.FromMilliseconds(500))
                return;
            _lastCustomizeUpdate = DateTime.UtcNow;
            string data = string.Empty;
            var activeProfileId = PsuPlugin.CustomizeService.GetActiveProfileOnCharacter(Player.ObjectIndex);
            if (activeProfileId.Item1 > 0 || activeProfileId.Item2 == null || activeProfileId.Item2 == Guid.Empty)
            {
                Svc.Log.Debug("Failed to get active C+ profile.");
            }
            else
            {
                var customizeData =
                    PsuPlugin.CustomizeService.GetCustomizeProfileByUniqueId(activeProfileId.Item2.Value);
                if (customizeData.Item1 > 0 || string.IsNullOrEmpty(customizeData.Item2))
                {
                    Svc.Log.Warning("Failed to get customize data.");
                }
                else
                {
                    data = customizeData.Item2;
                }
            }

            var pack = StarPackReference.GetDataPack();
            if (pack == null) return;
            var cData = new CustomizeData()
            {
                CustomizeState = data,
                LastUpdatedUtc = DateTime.UtcNow
            };

            var changed = CustomizeData == null ||
                          !string.Equals(CustomizeData.CustomizeState, cData.CustomizeState, StringComparison.Ordinal);
            if (changed)
            {
                CustomizeData = cData;
                var cLoc = CustomizeData.GetPath(pack.DataPath);
                var encodedCustomize = Base64Util.ToBase64(CustomizeData);
                Task.Run(async () =>
                {
                    await WriteText(cLoc, encodedCustomize);
                    Svc.Log.Debug("Updated Customize+ data on disk.");
                });
            }
        }
        catch (IpcNotReadyError ex)
        {
            Svc.Log.Warning("Customize+ plugin not ready.");
        }
    }

    public void UpdateGlamData()
    {
        try
        {
            if (Player == null)
                return;
            if (DateTime.UtcNow - _lastGlamUpdate < TimeSpan.FromMilliseconds(500))
                return;
            _lastGlamUpdate = DateTime.UtcNow;
            var glamState = PsuPlugin.GlamourerService.GetStateBase64.Invoke(Player.ObjectIndex);
            if (glamState.Item2 == null)
            {
                Svc.Log.Warning("Failed to get glamourer state.");
                return;
            }

            var pack = StarPackReference.GetDataPack();
            if (pack == null) return;

            var glamData = new GlamourerData()
            {
                GlamState = glamState.Item2
            };

            var changed = GlamourerData == null ||
                          !string.Equals(GlamourerData.GlamState, glamData.GlamState, StringComparison.Ordinal);
            if (changed)
            {
                GlamourerData = glamData;
                var glamourerLoc = GlamourerData.GetPath(pack.DataPath);
                var encodedGlamourer = Base64Util.ToBase64(GlamourerData);
                Task.Run(async () =>
                {
                    await WriteText(glamourerLoc, encodedGlamourer);
                    Svc.Log.Debug("Updated glamourer data on disk.");
                });
            }
        }
        catch (IpcNotReadyError ex)
        {
            Svc.Log.Warning("Glamourer plugin not ready.");
        }
    }

    public void UpdateBasicData()
    {
        if (Player == null)
            return;
        if (DateTime.UtcNow - _lastBasicUpdate < TimeSpan.FromMilliseconds(500))
            return;
        _lastBasicUpdate = DateTime.UtcNow;
        var pack = StarPackReference.GetDataPack();
        if (pack == null) return;

        var newBasic = new BasicData
        {
            PlayerName = Player.Name.TextValue,
            WorldId = Player.HomeWorld.RowId
        };

        var changed = Data?.PlayerName != newBasic.PlayerName || Data?.WorldId != newBasic.WorldId;
        if (changed)
        {
            Data = newBasic;
            var playerDataLoc = Data.GetPath(pack.DataPath);
            var encodedData = Base64Util.ToBase64(Data);
            Task.Run(async () =>
            {
                await WriteText(playerDataLoc, encodedData);
                Svc.Log.Debug("Updated basic info on disk.");
            });
        }
    }

    private sealed record PenumbraComputeSnapshot(
        string FilesPath,
        string DataPath,
        string MetaManipulations,
        IReadOnlyDictionary<string, IReadOnlyList<string>> ResourcePaths,
        IReadOnlyList<(string GamePath, string RealPath)> Swaps
    );

    public void UpdateTransientData(string gamePath, string realPath)
    {
        if (Player == null)
            return;
        var ext = Path.GetExtension(realPath);
        // ReSharper disable once PossibleUnintendedLinearSearchInSet
        if (AllowedFileExtensions.AlwaysExclude.Contains(ext, StringComparer.OrdinalIgnoreCase)) return;
        var normalizedGamePath = NormalizePenumbraPath(gamePath);
        var normalizedRealPath = NormalizePenumbraPath(realPath);
        if (normalizedGamePath == null || normalizedRealPath == null)
            return;
        if (string.Equals(normalizedRealPath, normalizedGamePath))
            return;
        if (PsuPlugin.Configuration.TransientFilesData.TryGetValue(realPath, out var transientData))
        {
            transientData.Add(normalizedGamePath);
            PsuPlugin.Configuration.TransientFilesData[realPath] = transientData;
        }
        else
        {
            PsuPlugin.Configuration.TransientFilesData[realPath] =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    normalizedGamePath
                };
        }
    }

    public readonly List<ushort> ApplicableObjectIndexes = new List<ushort>();

    public void UpdatePenumbraData()
    {
        try
        {
            if (Player == null)
                return;
            if (DateTime.UtcNow - _lastPenumbraUpdate < TimeSpan.FromMilliseconds(500))
                return;
            _lastPenumbraUpdate = DateTime.UtcNow;

            // Phase A: gather snapshot on framework thread (no IO)
            var dataPack = StarPackReference.GetDataPack();
            if (dataPack == null)
                return;
            var filesPath = dataPack.FilesPath;
            var dataPath = dataPack.DataPath;

            var manips = PsuPlugin.PenumbraService.GetPlayerMetaManipulations.Invoke();

            var playerResources = PsuPlugin.PenumbraService.GetPlayerResourcePaths.Invoke();
            ApplicableObjectIndexes.Clear();
            ApplicableObjectIndexes.AddRange(playerResources.Keys);
            var resourcePathsArr = playerResources.Values.ToArray();
            if (resourcePathsArr.Length == 0)
            {
                Svc.Log.Warning("Failed to get character resource paths from Penumbra.");
                return;
            }

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

            var ct = _penumbraCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    foreach (var (realPath, gamePaths) in PsuPlugin.Configuration.TransientFilesData)
                    {
                        if (ct.IsCancellationRequested)
                            return;
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

                    var resourcePaths = resolvedPaths
                        .Where(kvp => File.Exists(kvp.Key))
                        .GroupBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            g => g.Key,
                            IReadOnlyList<string> (g) => g.SelectMany(kvp => kvp.Value)
                                .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                            StringComparer.OrdinalIgnoreCase
                        );

                    var swaps = resolvedPaths
                        .Where(kvp => !File.Exists(kvp.Key))
                        .SelectMany(kvp => kvp.Value.Where(v => !string.Equals(v, kvp.Key))
                            .Select(gp => (GamePath: gp, RealPath: kvp.Key)))
                        .ToList();

                    var snapshot = new PenumbraComputeSnapshot(
                        filesPath,
                        dataPath,
                        manips,
                        resourcePaths,
                        swaps
                    );

                    var result = await ComputePenumbraAsync(snapshot, ct).ConfigureAwait(false);

                    var changed =
                        PenumbraData == null
                        || !string.Equals(PenumbraData.MetaManipulations, result.MetaManipulations,
                            StringComparison.Ordinal)
                        || !UnorderedEqualByKey(PenumbraData.Files, result.Files, FileKey)
                        || !UnorderedEqualByKey(PenumbraData.FileSwaps, result.FileSwaps, SwapKey);

                    if (!changed)
                        return;

                    PenumbraData = result;
                    var penumbraLoc = PenumbraData.GetPath(snapshot.DataPath);
                    var encoded = Base64Util.ToBase64(PenumbraData);
                    await WriteText(penumbraLoc, encoded);
                    Svc.Log.Debug("Updated penumbra data on disk.");
                    EzConfig.Save();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Svc.Log.Error($"Penumbra compute failed: {ex}");
                }
            }, ct);
        }
        catch (IpcNotReadyError)
        {
            Svc.Log.Warning("Penumbra plugin not ready.");
        }
    }

    private async Task<PenumbraData> ComputePenumbraAsync(PenumbraComputeSnapshot s, CancellationToken ct)
    {
        try
        {
            async Task<CustomRedirect?> ProcessOne(string localPath, IReadOnlyList<string> gamePaths)
            {
                await _semaphoreSlim.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    ct.ThrowIfCancellationRequested();
                    if (!File.Exists(localPath)) return null;
                    var info = new FileInfo(localPath);
                    bool cacheHit;
                    byte[] hash;
                    lock (FileHashCache)
                    {
                        cacheHit = FileHashCache.TryGetValue(localPath, out var entry) &&
                                   entry.LastWriteUtc == info.LastWriteTimeUtc && entry.Length == info.Length;
                        hash = cacheHit ? entry.Hash : [];
                    }

                    byte[]? data = null;
                    if (!cacheHit)
                    {
                        data = await File.ReadAllBytesAsync(localPath, ct).ConfigureAwait(false);
                        using var sha = SHA256.Create();
                        hash = sha.ComputeHash(data);
                        lock (FileHashCache)
                            FileHashCache[localPath] = (info.LastWriteTimeUtc, info.Length, hash);
                    }

                    var ext = Path.GetExtension(localPath);
                    var redirect = new CustomRedirect(hash)
                    {
                        FileExtension = string.IsNullOrWhiteSpace(ext)
                            ? Path.GetExtension(gamePaths.FirstOrDefault() ?? string.Empty)
                            : ext,
                        ApplicableGamePaths = gamePaths.ToList()
                    };
                    var redirectPath = redirect.GetPath(s.FilesPath);
                    if (!File.Exists(redirectPath))
                    {
                        data ??= await File.ReadAllBytesAsync(localPath, ct).ConfigureAwait(false);
                        var dir = Path.GetDirectoryName(redirectPath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        await File.WriteAllBytesAsync(redirectPath, data, ct).ConfigureAwait(false);
                    }

                    return redirect;
                }
                catch (Exception ex)
                {
                    Svc.Log.Error($"Error processing file {localPath}: {ex.Message}");
                    return null;
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
            }

            var fileTasks = s.ResourcePaths.Select(kv => ProcessOne(kv.Key, kv.Value));
            var files = (await Task.WhenAll(fileTasks).ConfigureAwait(false)).OfType<CustomRedirect>().ToList();
            var fileSwaps = s.Swaps.Select(t => new AssetSwap(t.GamePath, t.RealPath)).ToList();
            return new PenumbraData
            {
                Files = files,
                FileSwaps = fileSwaps,
                MetaManipulations = s.MetaManipulations,
                LastUpdatedUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error during Penumbra data computation: {ex}");
            throw;
        }
    }

    public void UpdateHeelsData()
    {
        try
        {
            if (Player == null)
                return;
            if (DateTime.UtcNow - _lastHeelsUpdate < TimeSpan.FromMilliseconds(500))
                return;
            _lastHeelsUpdate = DateTime.UtcNow;
            var heelsState = PsuPlugin.SimpleHeelsService.GetLocalPlayer();
            var pack = StarPackReference.GetDataPack();
            if (pack == null) return;
            var heelsData = new HeelsData()
            {
                LastUpdatedUtc = DateTime.UtcNow,
                HeelsState = heelsState
            };
            var changed = HeelsData == null ||
                          !string.Equals(HeelsData.HeelsState, heelsData.HeelsState, StringComparison.Ordinal);
            if (changed)
            {
                HeelsData = heelsData;
                var cLoc = HeelsData.GetPath(pack.DataPath);
                var encodedHeels = Base64Util.ToBase64(HeelsData);
                Task.Run(async () =>
                {
                    await WriteText(cLoc, encodedHeels);
                    Svc.Log.Debug("Updated Heels data on disk.");
                });
            }
        }
        catch (IpcNotReadyError ex)
        {
            Svc.Log.Warning("SimpleHeels plugin not ready.");
        }
    }

    const long giggleBit = 1_073_741_824L;

    public long MinimumRequiredDataPackSize
    {
        get
        {
            var filesPath = StarPackReference.GetDataPack()?.FilesPath ?? string.Empty;
            if (string.IsNullOrEmpty(filesPath))
                return 0L;
            return PenumbraData?.Files.Sum(f =>
            {
                var path = f.GetPath(filesPath);
                if (File.Exists(path))
                {
                    try
                    {
                        var info = new FileInfo(path);
                        return info.Length;
                    }
                    catch
                    {
                        return 0L;
                    }
                }

                return 0L;
            }) ?? 0L;
        }
    }

    public sealed override IPlayerCharacter? Player { get; set; }
}