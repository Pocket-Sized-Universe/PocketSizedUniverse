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
    }

    private DateTime _lastMoodlesUpdate = DateTime.MinValue;
    private DateTime _lastHonorificUpdate = DateTime.MinValue;
    private DateTime _lastCustomizeUpdate = DateTime.MinValue;
    private DateTime _lastGlamUpdate = DateTime.MinValue;
    private DateTime _lastBasicUpdate = DateTime.MinValue;
    private DateTime _lastPenumbraUpdate = DateTime.MinValue;

    // Penumbra heavy compute job control
    private CancellationTokenSource? _penumbraCts;
    private Task? _penumbraJob;
    private int _penumbraGen;

    private async Task WriteText(string path, string content)
    {
        try
        {
            var dir = Path.GetDirectoryName(path)!;
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
        if (Player == null)
            return;
        if (DateTime.UtcNow - _lastMoodlesUpdate < TimeSpan.FromMilliseconds(500))
            return;
        _lastMoodlesUpdate = DateTime.UtcNow;
        var moodles = PsuPlugin.MoodlesService.GetStatusManager(Player.Address);

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
            var cLoc = MoodlesData.GetPath(StarPackReference.GetDataPack()!.DataPath);
            var encodedMoodles = Base64Util.ToBase64(MoodlesData);
            Task.Run(async () =>
            {
                await WriteText(cLoc, encodedMoodles);
                Svc.Log.Debug("Updated Moodles data on disk.");
            });
        }
    }

    public void UpdateHonorificData()
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

        var changed = HonorificData == null ||
                      !string.Equals(HonorificData.Title, honorificData.Title, StringComparison.Ordinal);
        if (changed)
        {
            HonorificData = honorificData;
            var cLoc = HonorificData.GetPath(StarPackReference.GetDataPack()!.DataPath);
            var encodedHonorific = Base64Util.ToBase64(HonorificData);
            Task.Run(async () =>
            {
                await WriteText(cLoc, encodedHonorific);
                Svc.Log.Debug("Updated Honorific data on disk.");
            });
        }
    }

    public void UpdateCustomizeData()
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

        var changed = CustomizeData == null ||
                      !string.Equals(CustomizeData.CustomizeState, cData.CustomizeState, StringComparison.Ordinal);
        if (changed)
        {
            CustomizeData = cData;
            var cLoc = CustomizeData.GetPath(StarPackReference.GetDataPack()!.DataPath);
            var encodedCustomize = Base64Util.ToBase64(CustomizeData);
            Task.Run(async () =>
            {
                await WriteText(cLoc, encodedCustomize);
                Svc.Log.Debug("Updated Customize+ data on disk.");
            });
        }
    }

    public void UpdateGlamData()
    {
        if (Player == null)
            return;
        if (DateTime.UtcNow - _lastGlamUpdate < TimeSpan.FromMilliseconds(500))
            return;
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

        var changed = GlamourerData == null ||
                      !string.Equals(GlamourerData.GlamState, glamData.GlamState, StringComparison.Ordinal);
        if (changed)
        {
            GlamourerData = glamData;
            var glamourerLoc = GlamourerData.GetPath(StarPackReference.GetDataPack()!.DataPath);
            var encodedGlamourer = Base64Util.ToBase64(GlamourerData);
            Task.Run(async () =>
            {
                await WriteText(glamourerLoc, encodedGlamourer);
                Svc.Log.Debug("Updated glamourer data on disk.");
            });
        }
    }

    public void UpdateBasicData()
    {
        if (Player == null)
            return;
        if (DateTime.UtcNow - _lastBasicUpdate < TimeSpan.FromMilliseconds(500))
            return;
        _lastBasicUpdate = DateTime.UtcNow;

        var newBasic = new BasicData
        {
            PlayerName = Player.Name.TextValue,
            WorldId = Player.HomeWorld.RowId
        };

        var changed = Data?.PlayerName != newBasic.PlayerName || Data?.WorldId != newBasic.WorldId;
        if (changed)
        {
            Data = newBasic;
            var playerDataLoc = Data.GetPath(StarPackReference.GetDataPack()!.DataPath);
            var encodedData = Base64Util.ToBase64(Data);
            Task.Run(async () =>
            {
                await WriteText(playerDataLoc, encodedData);
                Svc.Log.Debug("Updated basic info on disk.");
            });
        }
    }

    private sealed record PenumbraComputeSnapshot(
        int ObjectIndex,
        string FilesPath,
        string DataPath,
        string MetaManipulations,
        IReadOnlyDictionary<string, IReadOnlyList<string>> ResourcePaths,
        IReadOnlyList<(string GamePath, string RealPath)> Swaps
    );

    public void UpdatePenumbraData()
    {
        if (Player == null)
            return;
        if (DateTime.UtcNow - _lastPenumbraUpdate < TimeSpan.FromMilliseconds(500))
            return;

        // Phase A: gather snapshot on framework thread (no IO)
        var dataPack = StarPackReference.GetDataPack();
        if (dataPack == null)
            return;
        var filesPath = dataPack.FilesPath;
        var dataPath = dataPack.DataPath;

        var manips = Svc.Framework.RunOnFrameworkThread(() => PsuPlugin.PenumbraService.GetPlayerMetaManipulations.Invoke()).ConfigureAwait(false).GetAwaiter().GetResult();

        var resourcePathsArr = Svc.Framework.RunOnFrameworkThread(() => PsuPlugin.PenumbraService.GetGameObjectResourcePaths.Invoke(Player.ObjectIndex)).ConfigureAwait(false).GetAwaiter().GetResult();
        if (resourcePathsArr.Length == 0)
        {
            Svc.Log.Warning("Failed to get character resource paths from Penumbra.");
            return;
        }

        var resolvedPaths = resourcePathsArr[0];
        if (resolvedPaths == null || resolvedPaths.Count == 0)
            return;

        var resourcePaths = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var kv in resolvedPaths)
        {
            var localPath = kv.Key;
            var gamePaths = kv.Value?.ToList() ?? new List<string>();
            if (!File.Exists(localPath))
                continue;
            var ext = Path.GetExtension(localPath);
            var allowedGamePaths = gamePaths.Where(gp => AllowedFileExtensions.IsAllowed(gp, ext)).ToList();
            if (allowedGamePaths.Count == 0)
                continue;
            resourcePaths[localPath] = allowedGamePaths;
        }

        var swaps = new List<(string GamePath, string RealPath)>();
        var penumbraResourceTree = PsuPlugin.PenumbraService.GetPlayerResourceTrees.Invoke();
        foreach (var tree in penumbraResourceTree)
        {
            foreach (var node in tree.Value.Nodes.Where(n => !File.Exists(n.ActualPath)))
            {
                if (node.GamePath == null) continue;
                var ext = Path.GetExtension(node.ActualPath);
                if (!AllowedFileExtensions.IsAllowed(node.GamePath, ext)) continue;
                swaps.Add((NormalizePenumbraPath(node.GamePath)!, NormalizePenumbraPath(node.ActualPath)!));
            }
        }

        var snapshot = new PenumbraComputeSnapshot(
            Player.ObjectIndex,
            filesPath,
            dataPath,
            manips,
            resourcePaths,
            swaps
        );

        // Phase B: coalesce and run heavy compute off-thread
        Interlocked.Increment(ref _penumbraGen);
        _penumbraCts?.Cancel();
        _penumbraCts = new CancellationTokenSource();
        var thisGen = _penumbraGen;
        var ct = _penumbraCts.Token;

        _penumbraJob = Task.Run(async () =>
        {
            try
            {
                var result = await ComputePenumbraAsync(snapshot, ct).ConfigureAwait(false);
                // Phase C: apply on framework thread

                if (thisGen != _penumbraGen || ct.IsCancellationRequested)
                    return;

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

    private static async Task<PenumbraData> ComputePenumbraAsync(PenumbraComputeSnapshot s, CancellationToken ct)
    {
        var files = new List<CustomRedirect>();
        foreach (var kv in s.ResourcePaths)
        {
            ct.ThrowIfCancellationRequested();
            var localPath = kv.Key;
            var gamePaths = kv.Value;
            if (!File.Exists(localPath))
                continue;

            try
            {
                var data = await File.ReadAllBytesAsync(localPath, ct).ConfigureAwait(false);
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(data);
                var ext = Path.GetExtension(localPath);
                var redirected = new CustomRedirect(hash)
                {
                    FileExtension = string.IsNullOrWhiteSpace(ext)
                        ? Path.GetExtension(gamePaths.FirstOrDefault() ?? string.Empty)
                        : ext,
                    ApplicableGamePaths = gamePaths.ToList()
                };

                var redirectPath = redirected.GetPath(s.FilesPath);
                if (!File.Exists(redirectPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(redirectPath)!);
                    await File.WriteAllBytesAsync(redirectPath, data, ct).ConfigureAwait(false);
                }

                files.Add(redirected);
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Error processing file {localPath}: {ex.Message}");
            }
        }

        var fileSwaps = s.Swaps
            .Select(t => new AssetSwap(t.GamePath, t.RealPath))
            .ToList();

        return new PenumbraData
        {
            Files = files,
            FileSwaps = fileSwaps,
            MetaManipulations = s.MetaManipulations,
            LastUpdatedUtc = DateTime.UtcNow
        };
    }

    public sealed override IPlayerCharacter? Player { get; set; }
}