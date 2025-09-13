using Newtonsoft.Json;

namespace PocketSizedUniverse.Models.Syncthing.Models.Response;

public class PingResponse
{
    public bool Ok => Ping == "pong";
    [JsonProperty("ping")]
    private string Ping { get; set; }
}