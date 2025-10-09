using Newtonsoft.Json;

namespace PocketSizedUniverse.Models.Syncthing.Models.Response;

public class Event
{
    [JsonProperty("id")]
    public int Id { get; set; }
    [JsonProperty("globalID")]
    public int GlobalId { get; set; }
    
    [JsonProperty("type")]
    private string _typeString { get; set; }
    [JsonIgnore]
    public EventType Type => (EventType)Enum.Parse(typeof(EventType), _typeString);
    
    [JsonProperty("data")]
    public required object Data { get; set; }
    
    [JsonProperty("time")]
    public DateTime Time { get; set; }
}