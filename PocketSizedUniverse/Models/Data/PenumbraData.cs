using System.Collections.Concurrent;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models.Mods;
using PocketSizedUniverse.Services;

namespace PocketSizedUniverse.Models.Data;

public class PenumbraData : IDataFile, IEquatable<PenumbraData>
{
    public static PenumbraData? LoadFromDisk(string basePath)
    {
        var path = Path.Combine(basePath, Filename);
        if (!File.Exists(path))
        {
            return null;
        }

        var data = File.ReadAllText(path);
        return Base64Util.FromBase64<PenumbraData>(data);
    }

    public int Version { get; set; } = 2;
    public static string Filename { get; } = "Penumbra.dat";

    // Aggregated, per-character Penumbra state (matching Mare's model)
    public List<CustomRedirect> Files { get; set; } = new();
    public List<AssetSwap> FileSwaps { get; set; } = new();
    public string MetaManipulations { get; set; } = string.Empty;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;

    // Runtime-only, precomputed mapping prepared on a background thread.
    [JsonIgnore] public Dictionary<string, string>? PreparedPaths { get; set; }

    public void PreparePaths(string filesPath, string dataPackLabel)
    {
        Dictionary<string, string> paths = new();
        foreach (var f in Files)
        {
            var localFilePath = f.GetPath(filesPath);
            if (!File.Exists(localFilePath))
                continue;

            if (IsFileSafeToUse(localFilePath, dataPackLabel))
            {
                foreach (var gamePath in f.ApplicableGamePaths.Where(gamePath => !string.IsNullOrWhiteSpace(gamePath)))
                {
                    paths[gamePath] = localFilePath;
                }
            }
            else
            {
                PsuPlugin.ClamScanProcess?.EnqueuePath(localFilePath);
            }
        }

        foreach (var s in FileSwaps)
        {
            if (string.IsNullOrWhiteSpace(s.GamePath) || string.IsNullOrWhiteSpace(s.RealPath)) continue;
            paths[s.GamePath] = s.RealPath;
        }

        PreparedPaths = paths;
    }
    
    private readonly ConcurrentBag<string> _virusAlertedFiles = new();
    private bool IsFileSafeToUse(string localFilePath, string dataPackLabel)
    {
        if (!PsuPlugin.Configuration.EnableVirusScanning)
            return true;

        if (!PsuPlugin.Configuration.ScanResults.TryGetValue(localFilePath, out var scanResult))
            return false;

        var fileInfo = new FileInfo(localFilePath);
        if (scanResult.ScanTimeUtc > fileInfo.LastWriteTimeUtc && 
               scanResult.Result == ScanResult.ResultType.Clean)
            return true;
        if (!_virusAlertedFiles.Contains(localFilePath))
        {
            _virusAlertedFiles.Add(localFilePath);
            Svc.Chat.PrintError($"[PSU] CRITICAL ALERT: DataPack {dataPackLabel} contains malware: {localFilePath}");
        }
        return false;
    }

    public bool Equals(PenumbraData? obj)
    {
        if (obj == null) return false;
        return UnorderedEqualByKey(Files, obj.Files, f => CanonicalPath(f.FileName))
               && UnorderedEqualByKey(FileSwaps, obj.FileSwaps,
                   s => CanonicalPath(s.GamePath) + "->" + CanonicalPath(s.RealPath))
               && MetaManipulations == obj.MetaManipulations;
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);

    private static string CanonicalPath(string? path)
        => (path ?? string.Empty).Replace('\\', '/').Trim();

    private static bool UnorderedEqualByKey<T>(IEnumerable<T> a, IEnumerable<T> b, Func<T, string> keySelector)
    {
        var ak = a.Select(keySelector).OrderBy(x => x, StringComparer.Ordinal);
        var bk = b.Select(keySelector).OrderBy(x => x, StringComparer.Ordinal);
        return ak.SequenceEqual(bk, StringComparer.Ordinal);
    }

    public (bool Applied, string Result) ApplyData(IPlayerCharacter player, params object?[] args)
    {
        try
        {
            var collId = args[0] as Guid?;
            if (collId != null)
            {
                PsuPlugin.PenumbraService.DeleteTemporaryCollection.Invoke(collId.Value);
            }

            PsuPlugin.PenumbraService.CreateTemporaryCollection.Invoke(
                "PocketSizedUniverse", "PSU_" + Id, out var newColl);
            collId = newColl;

            var metaModName = $"PSU_Meta_{Id}";
            PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(metaModName, collId.Value, 0);
            if (!string.IsNullOrEmpty(MetaManipulations))
            {
                PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(
                    metaModName, collId.Value, new Dictionary<string, string>(), MetaManipulations, 0);
            }

            var paths = PreparedPaths ?? new Dictionary<string, string>();

            var fileModName = $"PSU_File_{Id}";
            PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(fileModName, collId.Value, 0);
            if (paths.Count > 0)
            {
                PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(fileModName, collId.Value, paths,
                    string.Empty, 0);
            }

            PsuPlugin.PenumbraService.AssignTemporaryCollection.Invoke(collId.Value, player.ObjectIndex);
            PsuPlugin.PenumbraService.RedrawObject.Invoke(player.ObjectIndex);
            return (true, collId.Value.ToString());
        }
        catch (Exception ex)
        {
            PsuPlugin.PlayerDataService.ReportMissingPlugin(player.Name.TextValue, "Penumbra");
            return (false, string.Empty);
        }
    }
}