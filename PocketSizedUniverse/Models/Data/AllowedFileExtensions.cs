namespace PocketSizedUniverse.Models.Data;

public static class AllowedFileExtensions
{
    public static readonly HashSet<string> Normal = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mdl", ".tex", ".mtrl", ".sklb", ".eid", ".phyb", ".pbd", ".skp", ".shpk", ".tmb"
    };

    public static readonly HashSet<string> AlwaysExclude = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".imc"
    };

    public static readonly HashSet<string> Visuals = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avfx", ".atex"
    };

    public static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase)
    {
        ".scd"
    };

    public static readonly HashSet<string> Animations = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pap"
    };

    public static bool IsAllowed(string extension, SyncPermissions permissions)
    {
        if (string.IsNullOrEmpty(extension)) return false;
        if (AlwaysExclude.Contains(extension)) return false;
        if (Normal.Contains(extension)) return true;
        if (permissions.HasFlag(SyncPermissions.Visuals) && Visuals.Contains(extension)) return true;
        if (permissions.HasFlag(SyncPermissions.Sounds) && Audio.Contains(extension)) return true;
        if (permissions.HasFlag(SyncPermissions.Animations) && Animations.Contains(extension)) return true;
        return false;
    }
}
