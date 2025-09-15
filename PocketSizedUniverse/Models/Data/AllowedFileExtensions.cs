namespace PocketSizedUniverse.Models.Data;

public static class AllowedFileExtensions
{
    // Base set taken from Mare, excluding animations/sound by default
    public static readonly HashSet<string> Normal = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mdl", ".tex", ".mtrl", ".avfx", ".atex", ".sklb", ".eid", ".phyb", ".pbd", ".skp", ".shpk"
    };

    // These are excluded to match Mare’s filtering of pap/tmb/scd for player data
    public static readonly HashSet<string> AlwaysExclude = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pap", ".tmb", ".scd"
    };

    // For avfx/atex, Mare allows only for weapon/equipment; enforce same here
    public static bool IsAllowed(string gamePath, string extension)
    {
        if (string.IsNullOrEmpty(extension)) return false;
        if (AlwaysExclude.Contains(extension)) return false;

        if (string.Equals(extension, ".avfx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".atex", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(gamePath)) return false;
            var gp = gamePath.Replace('\\', '/');
            var ok = gp.Contains("/weapon/", StringComparison.OrdinalIgnoreCase)
                     || gp.Contains("/equipment/", StringComparison.OrdinalIgnoreCase);
            if (!ok) return false;
        }

        return Normal.Contains(extension);
    }
}
