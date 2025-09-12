namespace PocketSizedUniverse;

public enum SyncThingProcessStartType
{
    Start,
    Restart,
    Stop,
    Generate,
}

public static class SyncThingProcessStartTypeExtensions
{
    public static string ToArgument(this SyncThingProcessStartType type) => type switch
    {
        SyncThingProcessStartType.Start => "serve --no-browser ",
        SyncThingProcessStartType.Restart => "cli operations restart ",
        SyncThingProcessStartType.Stop => "cli operations shutdown ",
        SyncThingProcessStartType.Generate => "generate ",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}