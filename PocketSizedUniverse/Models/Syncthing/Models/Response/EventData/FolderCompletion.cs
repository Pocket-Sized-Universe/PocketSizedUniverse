using Newtonsoft.Json;

namespace PocketSizedUniverse.Models.Syncthing.Models.Response.EventData;

public class FolderCompletion
{
    [JsonProperty("completion")]
    public float Completion { get; set; }
    [JsonProperty("device")]
    public required string Device { get; set; }
    [JsonProperty("folder")]
    public required string Folder { get; set; }
    [JsonProperty("globalBytes")]
    public long GlobalBytes { get; set; }
    [JsonProperty("globalItems")]
    public long GlobalItems { get; set; }
    [JsonProperty("needBytes")]
    public long NeedBytes { get; set; }
    [JsonProperty("needDeletes")]
    public long NeedDeletes { get; set; }
    [JsonProperty("needItems")]
    public long NeedItems { get; set; }
    [JsonProperty("remoteState")]
    public required string RemoteState { get; set; }
    [JsonProperty("sequence")]
    public int Sequence { get; set; }
}