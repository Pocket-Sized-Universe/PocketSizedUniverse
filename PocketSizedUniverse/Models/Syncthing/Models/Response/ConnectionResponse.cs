using System.Collections.Generic;
using Newtonsoft.Json;

namespace PocketSizedUniverse.Models.Syncthing.Models.Response
{
    public class ConnectionsResponse
    {
        /// <summary>
        /// Dictionary of device IDs to their connection information.
        /// </summary>
        [JsonProperty("connections")]
        public Dictionary<string, Connection> Connections { get; set; } = new Dictionary<string, Connection>();

        /// <summary>
        /// Total connection statistics across all devices.
        /// </summary>
        [JsonProperty("total")]
        public ConnectionTotal Total { get; set; } = new ConnectionTotal();
    }
}