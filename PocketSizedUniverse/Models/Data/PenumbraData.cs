using Newtonsoft.Json;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models.Mods;

namespace PocketSizedUniverse.Models.Data;

public class PenumbraData : IDataFile
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
    public List<CustomRedirect> TransientFiles { get; set; } = new();
    public List<AssetSwap> TransientFileSwaps { get; set; } = new();
    public string MetaManipulations { get; set; } = string.Empty;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;

    // Runtime-only, precomputed mapping prepared on a background thread.
    [JsonIgnore]
    public Dictionary<string, string>? PreparedPaths { get; set; }

    public bool Equals(IWriteableData? x, IWriteableData? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Id == y.Id;
    }

    public int GetHashCode(IWriteableData obj)
    {
        return obj.Id.GetHashCode();
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

    public bool ApplyData(RemotePlayerData ctx, bool force = false)
    {
        // Compute change
        bool filesEq = true;
        bool swapsEq = true;
        bool transientFilesEq = true;
        bool transientSwapsEq = true;
        if (ctx.PenumbraData != null)
        {
            filesEq = UnorderedEqualByKey(ctx.PenumbraData.Files, Files, f =>
            {
                var b64 = Convert.ToBase64String(f.Hash);
                var ext = (f.FileExtension ?? string.Empty).Trim().ToLowerInvariant();
                var paths = f.ApplicableGamePaths
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(CanonicalPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
                return $"{b64}|{ext}|{string.Join(",", paths)}";
            });
            swapsEq = UnorderedEqualByKey(ctx.PenumbraData.FileSwaps, FileSwaps, s =>
            {
                var gp = CanonicalPath(s.GamePath).ToLowerInvariant();
                var rp = CanonicalPath(s.RealPath).ToLowerInvariant();
                return $"{gp}|{rp}";
            });
            transientFilesEq = UnorderedEqualByKey(ctx.PenumbraData.TransientFiles, TransientFiles, f =>
            {
                var b64 = Convert.ToBase64String(f.Hash);
                var ext = (f.FileExtension ?? string.Empty).Trim().ToLowerInvariant();
                var paths = f.ApplicableGamePaths
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(CanonicalPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
                return $"{b64}|{ext}|{string.Join(",", paths)}";
            });
            transientSwapsEq = UnorderedEqualByKey(ctx.PenumbraData.TransientFileSwaps, TransientFileSwaps, s =>
            {
                var gp = CanonicalPath(s.GamePath).ToLowerInvariant();
                var rp = CanonicalPath(s.RealPath).ToLowerInvariant();
                return $"{gp}|{rp}";
            });
        }

        var changed = ctx.PenumbraData == null
                      || !string.Equals(ctx.PenumbraData.MetaManipulations, MetaManipulations, StringComparison.Ordinal)
                      || !filesEq
                      || !swapsEq
                      || !transientFilesEq
                      || !transientSwapsEq
                      || ctx.AssignedCollectionId == null;
        if (!changed && !force)
            return false;

        // Cache new state always
        ctx.PenumbraData = this;
        if (ctx.Player == null)
            return false; // stash only

        // Ensure collection
        if (ctx.AssignedCollectionId == null)
        {
            PsuPlugin.PenumbraService.CreateTemporaryCollection.Invoke(
                "PocketSizedUniverse", "PSU_" + Id, out var newColl);
            ctx.AssignedCollectionId = newColl;
        }
        else
        {
            PsuPlugin.PenumbraService.DeleteTemporaryCollection.Invoke(ctx.AssignedCollectionId.Value);
            PsuPlugin.PenumbraService.CreateTemporaryCollection.Invoke(
                "PocketSizedUniverse", "PSU_" + Id, out var newColl);
            ctx.AssignedCollectionId = newColl;
        }

        var collectionId = ctx.AssignedCollectionId!.Value;

        // Meta manipulations
        var metaModName = $"PSU_Meta_{Id}";
        PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(metaModName, collectionId, 0);
        if (!string.IsNullOrEmpty(MetaManipulations))
        {
            PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(
                metaModName, collectionId, new Dictionary<string, string>(), MetaManipulations, 0);
        }

        // File redirects and swaps (precomputed; no disk IO on main thread)
        var paths = PreparedPaths ?? new Dictionary<string, string>();

        var fileModName = $"PSU_File_{Id}";
        PsuPlugin.PenumbraService.RemoveTemporaryMod.Invoke(fileModName, collectionId, 0);
        if (paths.Count > 0)
        {
            PsuPlugin.PenumbraService.AddTemporaryMod.Invoke(fileModName, collectionId, paths,
                string.Empty, 0);
        }

        PsuPlugin.PenumbraService.AssignTemporaryCollection.Invoke(collectionId, ctx.Player.ObjectIndex);
        PsuPlugin.PenumbraService.RedrawObject.Invoke(ctx.Player.ObjectIndex);
        return true;
    }
}
