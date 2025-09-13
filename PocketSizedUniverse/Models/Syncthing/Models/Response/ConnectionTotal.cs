using Newtonsoft.Json;

namespace PocketSizedUniverse.Models.Syncthing.Models.Response
{
    public class ConnectionTotal
    {
        /// <summary>
        /// The timestamp when the totals were calculated.
        /// </summary>
        [JsonProperty("at")]
        public DateTime At { get; set; }

        /// <summary>
        /// Total bytes received across all connections.
        /// </summary>
        [JsonProperty("inBytesTotal")]
        public long InBytesTotal { get; set; }

        /// <summary>
        /// Total bytes sent across all connections.
        /// </summary>
        [JsonProperty("outBytesTotal")]
        public long OutBytesTotal { get; set; }
    }
}