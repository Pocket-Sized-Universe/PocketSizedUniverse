using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;
using PocketSizedUniverse.Models.Mods;

namespace PocketSizedUniverse.Models.Data;

public abstract class PlayerData(StarPack starPack)
{
    /// <summary>
    /// Normalizes Penumbra paths to use forward slashes for cross-platform consistency.
    /// Penumbra GamePaths use forward slashes while ActualPaths may use backslashes on Windows.
    /// </summary>
    /// <param name="path">The path to normalize</param>
    /// <returns>A normalized path using forward slashes, or null if input was null</returns>
    protected static string? NormalizePenumbraPath(string? path)
    {
        if (path == null) return null;
        return string.Intern(path.Replace('\\', '/'));
    }

    // Helpers for canonical, order-insensitive comparisons used by Local/Remote variants
    protected static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    protected static string CanonicalPath(string? path)
    {
        return (NormalizePenumbraPath(path) ?? string.Empty).Trim();
    }

    protected static string FileKey(CustomRedirect f)
    {
        var b64 = Convert.ToBase64String(f.Hash);
        var ext = (f.FileExtension ?? string.Empty).Trim().ToLowerInvariant();
        var paths = (f.ApplicableGamePaths ?? new List<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(CanonicalPath)
            .Distinct(PathComparer)
            .OrderBy(p => p, PathComparer);
        return $"{b64}|{ext}|{string.Join(",", paths)}";
    }

    protected static string SwapKey(AssetSwap s)
    {
        var gp = CanonicalPath(s.GamePath).ToLowerInvariant();
        var rp = CanonicalPath(s.RealPath).ToLowerInvariant();
        return $"{gp}|{rp}";
    }

    protected static bool UnorderedEqualByKey<T>(IEnumerable<T> a, IEnumerable<T> b, Func<T, string> keySelector)
    {
        var ak = a.Select(keySelector).OrderBy(x => x, StringComparer.Ordinal);
        var bk = b.Select(keySelector).OrderBy(x => x, StringComparer.Ordinal);
        return ak.SequenceEqual(bk, StringComparer.Ordinal);
    }

    public abstract IPlayerCharacter? Player { get; set; }

    public BasicData? Data { get; protected set; }

    public PenumbraData? PenumbraData { get; protected set; }

    public GlamourerData? GlamourerData { get; protected set; }

    public CustomizeData? CustomizeData { get; protected set; }

    public HonorificData? HonorificData { get; protected set; }

    public StarPack StarPackReference { get; private set; } = starPack;
}