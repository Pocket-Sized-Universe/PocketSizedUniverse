namespace PocketSizedUniverse.Models.Data;

public static class AllowedFileExtensions
{
    // Base set taken from Mare, excluding animations/sound by default
    public static readonly HashSet<string> Normal = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mdl", ".tex", ".mtrl", ".avfx", ".atex", ".sklb", ".eid", ".phyb", ".pbd", ".skp", ".shpk", ".pap", ".tmb", ".scd"
    };

    // These are excluded to match Mare’s filtering of pap/tmb/scd for player data
    public static readonly HashSet<string> AlwaysExclude = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".imc"
    };

    // For avfx/atex, Mare allows only for weapon/equipment; enforce same here
    public static bool IsAllowed(string gamePath, string extension)
    {
        if (string.IsNullOrEmpty(extension)) return false;
        if (AlwaysExclude.Contains(extension)) return false;

        return Normal.Contains(extension);
    }
}
