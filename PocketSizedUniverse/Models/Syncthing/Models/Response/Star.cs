using Newtonsoft.Json;

namespace Syncthing.Models.Response
{
    public class Star
    {
        /// <summary>
        /// The star ID. (mandatory)
        /// </summary>
        [JsonProperty("deviceID")]
        public string StarId { get; set; }
        
        /// <summary>
        /// A friendly name for the star. (optional)
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
        
        // [JsonProperty("addresses")]

        /// <summary>
        /// Whether to use protocol compression when sending messages to this star.
        /// Possible values are from Type <see cref="CompressionType"/>.
        /// </summary>
        [JsonProperty("compression")]
        public string Compression { get; set; }
        
        /// <summary>
        /// The star certificate common name, if it is not the default “syncthing”.
        /// </summary>
        [JsonProperty("certName")]
        public string CertName { get; set; }

        /// <summary>
        /// Set to true if this star should be trusted as an introducer, i.e. we should copy their list of stars per
        /// folder when connecting.
        /// </summary>
        [JsonProperty("introducer")]
        public bool Introducer { get; set; }
        
        /// <summary>
        /// Set to true if you wish to follow only introductions and not de-introductions. For example, if this is set,
        /// we would not remove a star that we were introduced to even if the original introducer is no longer
        /// listing the remote star as known.
        /// </summary>
        [JsonProperty("skipIntroductionRemovals")]
        public bool SkipIntroductionRemovals { get; set; }
        
        /// <summary>
        /// Defines which star has introduced us to this star. Used only for following de-introductions.
        /// </summary>
        [JsonProperty("introducedBy")]
        public string IntroducedBy { get; set; }
        
        /// <summary>
        /// True if synchronization with this stars is (temporarily) suspended.
        /// </summary>
        [JsonProperty("paused")]
        public bool Paused { get; set; }
        
        /// <summary>
        /// If given, this restricts connections to this star to only this network.
        /// </summary>
        // [JsonProperty("allowedNetworks")]
        
        [JsonProperty("autoAcceptFolders")]
        public bool AutoAcceptFolders { get; set; }
        
        /// <summary>
        /// Maximum send rate to use for this star. Unit is kibibytes/second, despite the config name looking like
        /// kilobits/second.
        /// </summary>
        [JsonProperty("maxSendKbps")]
        public int MaxSendKbps { get; set; }
        
        /// <summary>
        /// Maximum receive rate to use for this star. Unit is kibibytes/second, despite the config name looking like
        /// kilobits/second.
        /// </summary>
        [JsonProperty("maxRecvKbps")]
        public int MaxRecvKbps { get; set; }
        
        // [JsonProperty("ignoredFolders")]

        /// <summary>
        /// Maximum amount of data to have outstanding in requests towards this star. Unit is kibibytes.
        /// </summary>
        [JsonProperty("maxRequestKiB")]
        public int MaxRequestKiB { get; set; }
        
        [JsonProperty("untrusted")]
        public bool Untrusted { get; set; }
        
        /// <summary>
        /// If set to a positive integer, the GUI will display an HTTP link to the IP address which is currently used
        /// for synchronization. Only the TCP port is exchanged for the value specified here. Note that any port
        /// forwarding or firewall settings need to be done manually and the link will probably not work for link-local
        /// IPv6 addresses because of modern browser limitations.
        /// </summary>
        [JsonProperty("remoteGUIPort")]
        public int RemoteGuiPort { get; set; }
    }
}