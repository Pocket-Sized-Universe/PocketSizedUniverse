using Newtonsoft.Json;

namespace PocketSizedUniverse.Models.Syncthing.Models.Response
{
    public class Connection
    {
        /// <summary>
        /// The address of the connection.
        /// </summary>
        [JsonProperty("address")]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// The timestamp when the connection was last active.
        /// </summary>
        [JsonProperty("at")]
        public DateTime At { get; set; }

        /// <summary>
        /// The client version of the connected device.
        /// </summary>
        [JsonProperty("clientVersion")]
        public string ClientVersion { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the connection is currently active.
        /// </summary>
        [JsonProperty("connected")]
        public bool Connected { get; set; }

        /// <summary>
        /// Total bytes received from this connection.
        /// </summary>
        [JsonProperty("inBytesTotal")]
        public long InBytesTotal { get; set; }

        /// <summary>
        /// Indicates whether this is a local connection.
        /// </summary>
        [JsonProperty("isLocal")]
        public bool IsLocal { get; set; }

        /// <summary>
        /// Total bytes sent to this connection.
        /// </summary>
        [JsonProperty("outBytesTotal")]
        public long OutBytesTotal { get; set; }

        /// <summary>
        /// Indicates whether the connection is paused.
        /// </summary>
        [JsonProperty("paused")]
        public bool Paused { get; set; }

        /// <summary>
        /// The timestamp when the connection was started.
        /// </summary>
        [JsonProperty("startedAt")]
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// The type of connection (e.g., "tcp-client").
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
    }
}