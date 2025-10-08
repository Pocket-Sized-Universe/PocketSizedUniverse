namespace PocketSizedUniverse.Models;

[Flags]
public enum SyncPermissions
{
    Sounds = 1,
    Animations = 2,
    Visuals = 4,
    All = Sounds | Animations | Visuals,
}